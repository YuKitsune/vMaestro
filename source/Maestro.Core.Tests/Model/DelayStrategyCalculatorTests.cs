using Maestro.Contracts.Shared;
using Maestro.Core.Model;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class DelayStrategyCalculatorTests
{
    static readonly TimeSpan PressureWindow = TimeSpan.FromMinutes(3);
    static readonly TimeSpan MaximumTmaPressure = TimeSpan.FromMinutes(4);
    static readonly TimeSpan ShortcutTimeToGain = TimeSpan.Zero;
    static readonly TimeSpan MaxLinearEnrouteDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// When no pressure trajectory is configured, <see cref="TrajectoryService"/> returns
    /// <c>TerminalTrajectory(ttg, ttg, ttg)</c> (pressure == normal == max). Both strategies
    /// must then allocate all delay to enroute, preserving the pre-branch behaviour where TMA
    /// delay did not exist.
    /// </summary>
    public class NoPressureTrajectory
    {
        static readonly TimeSpan NormalTtg = TimeSpan.FromMinutes(20);

        // No pressure window configured, so all delay must go to enroute
        static readonly TerminalTrajectory Trajectory = new(NormalTtg, NormalTtg, NormalTtg);
        static readonly EnrouteTrajectory EnrouteTrajectory = new(TimeSpan.FromMinutes(5), TimeSpan.Zero);

        [Fact]
        public void EarlyFlight_ReturnsExpedite_WithZeroEnrouteDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(-2);
            foreach (var strategy in new[] { DelayStrategy.EnrouteFirst, DelayStrategy.ApproachFirst })
            {
                var r = DelayStrategyCalculator.Compute(totalDelay, Trajectory, EnrouteTrajectory, strategy);
                r.ControlAction.ShouldBe(ControlAction.Expedite, $"strategy={strategy}");
                r.EnrouteDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}: early flights speed up in TMA, no enroute action");
                r.TerminalDelay.ShouldBe(totalDelay, $"strategy={strategy}: negative deviation absorbed in TMA");
            }
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.Zero;
            foreach (var strategy in new[] { DelayStrategy.EnrouteFirst, DelayStrategy.ApproachFirst })
            {
                var r = DelayStrategyCalculator.Compute(totalDelay, Trajectory, EnrouteTrajectory, strategy);
                r.ControlAction.ShouldBe(ControlAction.NoDelay, $"strategy={strategy}");
                r.EnrouteDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}");
                r.TerminalDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}");
            }
        }

        [Fact]
        public void SmallPositiveDelay_AllGoesToEnroute()
        {
            // Any positive delay with no TMA capacity must be assigned fully to enroute.
            var totalDelay = TimeSpan.FromMinutes(3);
            foreach (var strategy in new[] { DelayStrategy.EnrouteFirst, DelayStrategy.ApproachFirst })
            {
                var r = DelayStrategyCalculator.Compute(totalDelay, Trajectory, EnrouteTrajectory, strategy);
                r.EnrouteDelay.ShouldBe(totalDelay, $"strategy={strategy}: all delay to enroute when no TMA pressure configured");
                r.TerminalDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}: no TMA delay when pressure equals normal time to go");
            }
        }

        [Fact]
        public void DelayBeyondEnrouteCapacity_StillAllEnroute_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(10); // beyond max linear enroute delay of 5 minutes
            foreach (var strategy in new[] { DelayStrategy.EnrouteFirst, DelayStrategy.ApproachFirst })
            {
                var r = DelayStrategyCalculator.Compute(totalDelay, Trajectory, EnrouteTrajectory, strategy);
                r.ControlAction.ShouldBe(ControlAction.Holding, $"strategy={strategy}");
                r.EnrouteDelay.ShouldBe(totalDelay, $"strategy={strategy}: all delay to enroute even in holding");
                r.TerminalDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}: no TMA delay when no pressure configured");
            }
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(10)]
        public void DelaySplitSumsToTotal(int totalDelayMinutes)
        {
            var totalDelay = TimeSpan.FromMinutes(totalDelayMinutes);
            foreach (var strategy in new[] { DelayStrategy.EnrouteFirst, DelayStrategy.ApproachFirst })
            {
                var r = DelayStrategyCalculator.Compute(totalDelay, Trajectory, EnrouteTrajectory, strategy);
                (r.EnrouteDelay + r.TerminalDelay).ShouldBe(totalDelay, $"strategy={strategy}: enroute and terminal delay must sum to total delay");
            }
        }
    }

    public class EnrouteFirst
    {
        [Fact]
        public void EarlyFlight_WithinOneMintue_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(-1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute action for sub-minute early flights");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(-1), "deviation absorbed entirely in TMA");
        }

        [Fact]
        public void EarlyFlight_BeyondOneMinute_ReturnsExpedite()
        {
            var totalDelay = TimeSpan.FromMinutes(-1) - TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute action for early flights");
            r.TerminalDelay.ShouldBe(totalDelay, "deviation absorbed entirely in TMA");
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.Zero;
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute delay needed");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "no TMA delay needed");
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(3); // exactly at the pressure window boundary
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "delay within pressure window, no enroute action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "all delay absorbed as TMA pressure");
        }

        [Fact]
        public void DelayJustBeyondPressure_ReturnsSpeedReduction()
        {
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // just over pressure window
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "excess over pressure window assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at full pressure window");
        }

        [Fact]
        public void DelayAtMaxLinearEnrouteDelayBoundary_ReturnsSpeedReduction()
        {
            var totalDelay = TimeSpan.FromMinutes(8); // pressure window (3m) + max linear enroute delay (5m)
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at full pressure window");
        }

        [Fact]
        public void DelayBeyondMaxLinearEnrouteDelay_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // just over pressure window + max linear enroute delay
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "enroute capped at maximum linear delay");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1), "TMA absorbs remainder beyond enroute capacity");
        }

        [Fact]
        public void DelayAtMaximumTmaPressureBoundary_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(12); // pressure window (3m) + max linear enroute delay (5m) + max TMA pressure (4m)
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA at maximum pressure");
        }

        [Fact]
        public void DelayBeyondMaximumTmaPressure_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(1); // just over all linear capacity
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "excess beyond both TMA and enroute linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoPressureTrajectory_SubMinuteDelay_ReturnsNoDelay()
        {
            // Sub-minute delays always return NoDelay regardless of pressure configuration
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "sub-minute delay requires no controller action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromSeconds(1), "sub-minute delay absorbed in TMA");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondPressure_SkipsPathStretching_ReturnsSpeedReduction()
        {
            // With no additional TMA capacity beyond the pressure window, path stretching range is empty
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // just over pressure window
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "excess over pressure window assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at pressure window");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // With no additional TMA capacity beyond the pressure window, path stretching range is empty
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // just over pressure window + max linear enroute delay
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "no additional TMA capacity, excess requires holding");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at maximum");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_SubMinuteDelay_ReturnsNoDelay()
        {
            // Sub-minute delays always return NoDelay regardless of pressure configuration
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "sub-minute delay requires no controller action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromSeconds(1), "sub-minute delay absorbed in TMA");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // With no TMA capacity, all delay goes to enroute; path stretching goes directly to holding
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // just over max linear enroute delay
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "no TMA capacity, all delay in enroute including holding");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "no TMA pressure configured");
        }

        [Fact]
        public void WithShortcut_DelayAtResumeBoundary_ReturnsResume()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(5); // pressure window (3m) + shortcut (2m)
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2), "excess absorbed via enroute shortcut");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at full pressure window");
        }

        [Fact]
        public void WithShortcut_DelayJustBeyondResume_ReturnsSpeedReduction()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // just over pressure window + shortcut
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1), "shortcut exhausted, linear enroute delay required");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at full pressure window");
        }

        [Fact]
        public void WithNoMaxLinearEnrouteDelay_DelayBeyondPressure_TmaAbsorbsAll()
        {
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // just over pressure window + max TMA pressure
            var r = Compute(totalDelay, maxLinearEnrouteDelay: TimeSpan.Zero);
            // With no linear enroute capacity, path stretching range is empty; goes straight to holding
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "no linear enroute capacity, excess requires holding");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void DelaySplitSumsToTotal(int totalDelayMinutes)
        {
            var totalDelay = TimeSpan.FromMinutes(totalDelayMinutes);
            var r = Compute(totalDelay);
            (r.EnrouteDelay + r.TerminalDelay).ShouldBe(TimeSpan.FromMinutes(totalDelayMinutes), "enroute and terminal delay must sum to total delay");
        }

        static DelayDistribution Compute(
            TimeSpan totalDelay,
            TimeSpan? pressureWindow = null,
            TimeSpan? maximumTmaPressure = null,
            TimeSpan? shortCutTimeToGain = null,
            TimeSpan? maxLinearEnrouteDelay = null)
        {
            var effectivePressureWindow = pressureWindow ?? PressureWindow;
            var effectiveMaximumTmaPressure = maximumTmaPressure ?? MaximumTmaPressure;
            var terminalTrajectory = new TerminalTrajectory(TimeSpan.Zero, effectivePressureWindow, effectivePressureWindow + effectiveMaximumTmaPressure);
            var enrouteTrajectory = new EnrouteTrajectory(maxLinearEnrouteDelay ?? MaxLinearEnrouteDelay, shortCutTimeToGain ?? ShortcutTimeToGain);
            return DelayStrategyCalculator.Compute(totalDelay, terminalTrajectory, enrouteTrajectory, DelayStrategy.EnrouteFirst);
        }
    }

    public class ApproachFirst
    {
        [Fact]
        public void EarlyFlight_WithinOneMinute_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(-1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute action for sub-minute early flights");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(-1), "deviation absorbed entirely in TMA");
        }

        [Fact]
        public void EarlyFlight_BeyondOneMinute_ReturnsExpedite()
        {
            var totalDelay = TimeSpan.FromMinutes(-1) - TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute action for early flights");
            r.TerminalDelay.ShouldBe(totalDelay, "deviation absorbed entirely in TMA");
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.Zero;
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "no enroute delay needed");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "no TMA delay needed");
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(3); // exactly at the pressure window boundary
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "delay within pressure window, no enroute action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "all delay absorbed as TMA pressure");
        }

        [Fact]
        public void DelayBeyondPressure_WithinMaximumTmaPressure_ReturnsResume()
        {
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // just over pressure window
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "approach-first keeps delay in TMA up to maximum pressure");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1), "TMA absorbs all delay within maximum pressure");
        }

        [Fact]
        public void DelayAtMaximumTmaPressureBoundary_ReturnsResume()
        {
            var totalDelay = TimeSpan.FromMinutes(7); // pressure window (3m) + max TMA pressure (4m)
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "delay exactly at maximum TMA pressure, still absorbed in TMA");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA at maximum pressure boundary");
        }

        [Fact]
        public void DelayBeyondMaximumTmaPressure_WithinMaxLinearEnrouteDelay_ReturnsPathStretching()
        {
            // With no shortcut, speed reduction range is empty; path stretching starts immediately after max TMA pressure
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // just over max TMA pressure
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "excess beyond maximum TMA pressure assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void DelayAtMaxLinearEnrouteDelayBoundary_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(12); // pressure window (3m) + max TMA pressure (4m) + max linear enroute delay (5m)
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void DelayBeyondMaxLinearEnrouteDelay_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(1); // just over all linear capacity
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "both TMA and enroute linear capacity exhausted");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoPressureTrajectory_SubMinuteDelay_ReturnsNoDelay()
        {
            // Sub-minute delays always return NoDelay regardless of pressure configuration
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "sub-minute delay requires no controller action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromSeconds(1), "sub-minute delay absorbed in TMA");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondPressure_SkipsResume_ReturnsPathStretching()
        {
            // With no additional TMA capacity beyond the pressure window, resume range is empty
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // just over pressure window
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "no additional TMA capacity, excess goes to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA at maximum");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // With no additional TMA capacity beyond the pressure window, resume range is empty
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // just over pressure window + max linear enroute delay
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "both TMA and enroute linear capacity exhausted");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "TMA held at maximum");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_SubMinuteDelay_ReturnsNoDelay()
        {
            // Sub-minute delays always return NoDelay regardless of pressure configuration
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "sub-minute delay requires no controller action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromSeconds(1), "sub-minute delay absorbed in TMA");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // With no TMA capacity, all delay goes to enroute; path stretching goes directly to holding
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // just over max linear enroute delay
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "no TMA capacity, all delay in enroute including holding");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "no TMA pressure configured");
        }

        [Fact]
        public void WithShortcut_DelayAtSpeedReductionBoundary_ReturnsSpeedReduction()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(9); // pressure window (3m) + max TMA pressure (4m) + shortcut (2m)
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2), "excess absorbed via enroute shortcut");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void WithShortcut_DelayJustBeyondSpeedReduction_ReturnsPathStretching()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(1); // just over pressure window + max TMA pressure + shortcut
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1), "shortcut exhausted, linear enroute delay required");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoMaxLinearEnrouteDelay_LargeDelay_GoesToHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // just over max TMA pressure
            var r = Compute(totalDelay, maxLinearEnrouteDelay: TimeSpan.Zero);
            // With no linear enroute capacity, path stretching and speed reduction ranges are empty; goes to holding
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "TMA held at maximum pressure");
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "no linear enroute capacity, excess requires holding");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        public void DelaySplitSumsToTotal(int totalDelayMinutes)
        {
            var totalDelay = TimeSpan.FromMinutes(totalDelayMinutes);
            var r = Compute(totalDelay);
            (r.EnrouteDelay + r.TerminalDelay).ShouldBe(TimeSpan.FromMinutes(totalDelayMinutes), "enroute and terminal delay must sum to total delay");
        }

        static DelayDistribution Compute(
            TimeSpan totalDelay,
            TimeSpan? pressureWindow = null,
            TimeSpan? maximumTmaPressure = null,
            TimeSpan? shortCutTimeToGain = null,
            TimeSpan? maxLinearEnrouteDelay = null)
        {
            var effectivePressureWindow = pressureWindow ?? PressureWindow;
            var effectiveMaximumTmaPressure = maximumTmaPressure ?? MaximumTmaPressure;
            var terminalTrajectory = new TerminalTrajectory(TimeSpan.Zero, effectivePressureWindow, effectivePressureWindow + effectiveMaximumTmaPressure);
            var enrouteTrajectory = new EnrouteTrajectory(maxLinearEnrouteDelay ?? MaxLinearEnrouteDelay, shortCutTimeToGain ?? ShortcutTimeToGain);
            return DelayStrategyCalculator.Compute(totalDelay, terminalTrajectory, enrouteTrajectory, DelayStrategy.ApproachFirst);
        }
    }
}
