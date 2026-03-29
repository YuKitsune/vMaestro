using Maestro.Contracts.Shared;

namespace Maestro.Core.Model;

/// <summary>
///     The result of a delay distribution calculation.
/// </summary>
/// <param name="EnrouteDelay">Portion of delay to be absorbed in the enroute (ACC) phase.</param>
/// <param name="TmaDelay">Portion of delay to be absorbed in the approach (TMA) phase.</param>
/// <param name="ControlAction">The control action required to achieve the delay distribution.</param>
public record DelayDistribution(TimeSpan EnrouteDelay, TimeSpan TmaDelay, ControlAction ControlAction);

public static class DelayStrategyCalculator
{
    /// <summary>
    ///     Computes the delay distribution and control action for a flight.
    /// </summary>
    /// <param name="dT">Total delay (STA - ETA). Negative values indicate the flight is early.</param>
    /// <param name="p">Available approach pressure window (Trajectory.Pressure - Trajectory.TimeToGo).</param>
    /// <param name="dPmax">Additional delay available via max pressure (Trajectory.MaxPressure - Trajectory.Pressure).</param>
    /// <param name="sc">
    ///     Time savings available via enroute short-cut.
    ///     // TODO: Derive from enroute trajectory lookup (entry point x feeder fix). Currently always zero.
    /// </param>
    /// <param name="dCmax">
    ///     Maximum delay absorbable in the enroute phase via linear techniques.
    ///     // TODO: Derive from enroute trajectory lookup (entry point x feeder fix). Currently fixed at 5 minutes.
    /// </param>
    /// <param name="strategy">The delay distribution strategy configured for the airport.</param>
    public static DelayDistribution Compute(
        TimeSpan totalDelay,
        TerminalTrajectory terminalTrajectory,
        EnrouteTrajectory enrouteTrajectory,
        DelayStrategy strategy)
    {
        return strategy switch
        {
            DelayStrategy.EnrouteFirst => ComputeEnrouteFirst(totalDelay, terminalTrajectory, enrouteTrajectory),
            DelayStrategy.ApproachFirst => ComputeApproachFirst(totalDelay, terminalTrajectory, enrouteTrajectory),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null)
        };
    }

    /// <summary>
    ///     Returns the control action required for the given remaining delay, discarding the delay split.
    ///     Use this when updating a sequenced flight's status after its ETA changes.
    /// </summary>
    public static ControlAction GetControlAction(
        TimeSpan remainingDelay,
        TerminalTrajectory terminalTrajectory,
        EnrouteTrajectory enrouteTrajectory,
        DelayStrategy strategy)
        => Compute(remainingDelay, terminalTrajectory, enrouteTrajectory, strategy).ControlAction;

    // Enroute Delay Strategy: absorb delay in enroute phase first, then approach.
    //
    // | dT                                        | dC         | dP           | Action          |
    // |-------------------------------------------|------------|--------------|-----------------|
    // | dT < 0                                    | 0          | dT           | Expedite        |
    // | 0 <= dT <= P                              | 0          | dT           | NoDelay         |
    // | P < dT <= P + SC                          | dT - P     | P            | Resume          |
    // | P + SC < dT <= P + SC + dCmax             | dT - P     | P            | SpeedReduction  |
    // | P + SC + dCmax < dT <= P + SC + dCmax + dPmax | dCmax  | dT - dCmax   | PathStretching  |
    // | dT > P + SC + dCmax + dPmax               | dT-P-dPmax | P + dPmax    | Holding         |
    static DelayDistribution ComputeEnrouteFirst(TimeSpan totalDelay, TerminalTrajectory terminalTrajectory, EnrouteTrajectory enrouteTrajectory)
    {
        // That can be lost by slightly extending the normal trajectory
        var availablePressure = terminalTrajectory.PressureTimeToGo - terminalTrajectory.NormalTimeToGo;

        // The maximum amount of delay that can be absorbed within the TMA linearly (maximum pressure)
        var maxLinearTmaDelay = terminalTrajectory.MaxPressureTimeToGo - terminalTrajectory.NormalTimeToGo;

        // Needs to speed up
        if (totalDelay < TimeSpan.Zero)
            return new DelayDistribution(
                TimeSpan.Zero,
                totalDelay,
                ControlAction.Expedite);

        // If the delay required is within the TMA pressure, just absorb it within the TMA
        if (totalDelay <= availablePressure)
            return new DelayDistribution(
                TimeSpan.Zero,
                totalDelay,
                ControlAction.NoDelay);

        // Use the pressure in the TMA, absorb the rest in ENR
        if (totalDelay <= availablePressure + enrouteTrajectory.ShortCutTimeToGain)
            return new DelayDistribution(
                totalDelay - availablePressure,
                availablePressure,
                ControlAction.Resume);

        // Use the pressure in the TMA, absorb the rest in ENR
        if (totalDelay <= availablePressure + enrouteTrajectory.ShortCutTimeToGain + enrouteTrajectory.MaxLinearEnrouteDelay)
            return new DelayDistribution(
                totalDelay - availablePressure,
                availablePressure,
                ControlAction.SpeedReduction);

        // Max out the ENR delay, absorb the rest in the TMA
        // so long as the total delay won't exceed the TMAs max pressure
        if (totalDelay <= enrouteTrajectory.ShortCutTimeToGain + enrouteTrajectory.MaxLinearEnrouteDelay + maxLinearTmaDelay)
            return new DelayDistribution(
                enrouteTrajectory.MaxLinearEnrouteDelay,
                totalDelay - enrouteTrajectory.MaxLinearEnrouteDelay,
                ControlAction.PathStretching);

        // Both ENR and TMA max delays exceeded, holding required.
        // Delay as much as possible in the TMA, absorb the rest in ENR
        return new DelayDistribution(
            totalDelay - maxLinearTmaDelay,
            maxLinearTmaDelay,
            ControlAction.Holding);
    }

    // Approach Delay Strategy: absorb delay in approach phase first, then enroute.
    //
    // | dT                                            | dC          | dP        | Action          |
    // |-----------------------------------------------|-------------|-----------|-----------------|
    // | dT < 0                                        | 0           | dT        | Expedite        |
    // | 0 <= dT <= P                                  | 0           | dT        | NoDelay         |
    // | P < dT <= P + dPmax                           | 0           | dT        | Resume          |
    // | P + dPmax < dT <= P + dPmax + SC              | dT-P-dPmax  | P + dPmax | SpeedReduction  |
    // | P + dPmax + SC < dT <= P + dPmax + SC + dCmax | dT-P-dPmax  | P + dPmax | PathStretching  |
    // | dT > P + dPmax + SC + dCmax                   | dT-P-dPmax  | P + dPmax | Holding         |
    static DelayDistribution ComputeApproachFirst(TimeSpan totalDelay, TerminalTrajectory terminalTrajectory, EnrouteTrajectory enrouteTrajectory)
    {
        // That can be lost by slightly extending the normal trajectory
        var availablePressure = terminalTrajectory.PressureTimeToGo - terminalTrajectory.NormalTimeToGo;

        // The maximum amount of delay that can be absorbed within the TMA linearly (maximum pressure)
        var maxLinearTmaDelay = terminalTrajectory.MaxPressureTimeToGo - terminalTrajectory.NormalTimeToGo;

        // Needs to speed up
        if (totalDelay < TimeSpan.Zero)
            return new DelayDistribution(
                TimeSpan.Zero,
                totalDelay,
                ControlAction.Expedite);

        // If the delay required is within the TMA pressure, just absorb it within the TMA
        if (totalDelay <= availablePressure)
            return new DelayDistribution(
                TimeSpan.Zero,
                totalDelay,
                ControlAction.NoDelay);

        // If the delay required is within the max TMA pressure, just absorb it within the TMA
        if (totalDelay <= maxLinearTmaDelay)
            return new DelayDistribution(
                TimeSpan.Zero,
                totalDelay,
                ControlAction.Resume);

        // Absorb as much delay as possible in the TMA, absorb the rest in ENR
        if (totalDelay <= maxLinearTmaDelay + enrouteTrajectory.ShortCutTimeToGain)
            return new DelayDistribution(
                totalDelay - maxLinearTmaDelay,
                maxLinearTmaDelay,
                ControlAction.SpeedReduction);

        // Absorb as much delay as possible in the TMA, absorb the rest in ENR
        if (totalDelay <= maxLinearTmaDelay + enrouteTrajectory.ShortCutTimeToGain + enrouteTrajectory.MaxLinearEnrouteDelay)
            return new DelayDistribution(
                totalDelay - maxLinearTmaDelay,
                maxLinearTmaDelay,
                ControlAction.PathStretching);

        // Both ENR and TMA max delays exceeded, holding required.
        // Delay as much as possible in the TMA, absorb the rest in ENR
        return new DelayDistribution(
            totalDelay - maxLinearTmaDelay,
            maxLinearTmaDelay,
            ControlAction.Holding);
    }
}
