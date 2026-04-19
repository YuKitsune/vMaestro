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

        // PressureTTG = MaxPressureTTG = NormalTTG → availablePressure = 0, maxLinearTerminalDelay = 0
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
                r.TerminalDelay.ShouldBe(TimeSpan.Zero, $"strategy={strategy}: no TMA delay when pressure == normal TTG");
            }
        }

        [Fact]
        public void DelayBeyondEnrouteCapacity_StillAllEnroute_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(10); // beyond MaxLinearEnrouteDelay=5m
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
                (r.EnrouteDelay + r.TerminalDelay).ShouldBe(totalDelay, $"strategy={strategy}: dC + dP must equal total delay");
            }
        }
    }

    public class EnrouteFirst
    {
        [Fact]
        public void EarlyFlight_ReturnsExpedite()
        {
            var totalDelay = TimeSpan.FromMinutes(-1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: no enroute action for early flights");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(-1), "dP = totalDelay: deviation absorbed entirely in TMA");
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.Zero;
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: no enroute delay needed");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA delay needed");
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(3); // totalDelay = P
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: delay within pressure window, no enroute action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = totalDelay: all delay absorbed as TMA pressure");
        }

        [Fact]
        public void DelayJustBeyondPressure_ReturnsSpeedReduction()
        {
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // totalDelay = P + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P: excess over pressure window assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at full pressure window");
        }

        [Fact]
        public void DelayAtMaxLinearEnrouteDelayBoundary_ReturnsSpeedReduction()
        {
            var totalDelay = TimeSpan.FromMinutes(8); // totalDelay = P + SC + dCmax = 3 + 0 + 5
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "dC = totalDelay - P = 5m: enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at full pressure window");
        }

        [Fact]
        public void DelayBeyondMaxLinearEnrouteDelay_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // totalDelay = P + SC + dCmax + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "dC = dCmax: enroute capped at maximum linear delay");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1), "dP = totalDelay - dCmax: TMA absorbs remainder beyond enroute capacity");
        }

        [Fact]
        public void DelayAtMaximumTmaPressureBoundary_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(12); // totalDelay = P + SC + dCmax + dPmax = 3 + 0 + 5 + 4
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "dC = dCmax: enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = totalDelay - dCmax = 7m: TMA at maximum pressure");
        }

        [Fact]
        public void DelayBeyondMaximumTmaPressure_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(1); // totalDelay = P + SC + dCmax + dPmax + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: excess beyond both TMA and enroute linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoPressureTrajectory_SmallPositiveDelay_ReturnsSpeedReduction()
        {
            // When PressureWindow=0 and MaximumTmaPressure=0, any positive delay is immediately enroute
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay: no TMA pressure available, all delay assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no pressure trajectory configured");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondPressure_SkipsPathStretching_ReturnsSpeedReduction()
        {
            // dPmax = 0: PathStretching range is empty; SpeedReduction goes directly to Holding
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // totalDelay = P + 1s
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P: excess over pressure window assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at pressure window (P = max pressure, dPmax = 0)");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // dPmax = 0: PathStretching range is empty; goes directly from SpeedReduction to Holding
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // totalDelay = P + dCmax + 1s
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay - P: no additional TMA capacity (dPmax = 0), excess requires holding");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at maximum (P = max pressure)");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_SmallPositiveDelay_ReturnsSpeedReduction()
        {
            // P = 0 and dPmax = 0: any positive delay is immediately assigned to enroute
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay: no TMA capacity, all delay assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA pressure configured");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // P = 0 and dPmax = 0: PathStretching range is also empty; SpeedReduction goes directly to Holding
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // totalDelay = dCmax + 1s
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay: no TMA capacity, all delay in enroute including holding");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA pressure configured");
        }

        [Fact]
        public void WithShortcut_DelayAtResumeBoundary_ReturnsResume()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(5); // totalDelay = P + SC = 3 + 2
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2), "dC = totalDelay - P = 2m: excess absorbed via enroute shortcut");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at full pressure window");
        }

        [Fact]
        public void WithShortcut_DelayJustBeyondResume_ReturnsSpeedReduction()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // totalDelay = P + SC + 1s
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1), "dC = totalDelay - P: shortcut exhausted, linear enroute delay required");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at full pressure window");
        }

        [Fact]
        public void WithNoMaxLinearEnrouteDelay_DelayBeyondPressure_TmaAbsorbsAll()
        {
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // totalDelay = P + dPmax + 1s
            var r = Compute(totalDelay, maxLinearEnrouteDelay: TimeSpan.Zero);
            // With MaxLinearEnrouteDelay=0 the PathStretching range is empty; goes straight to Holding
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: no linear enroute capacity, excess requires holding");
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
            (r.EnrouteDelay + r.TerminalDelay).ShouldBe(TimeSpan.FromMinutes(totalDelayMinutes), "dC + dP must equal total delay");
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
        public void EarlyFlight_ReturnsExpedite()
        {
            var totalDelay = TimeSpan.FromMinutes(-1);
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: no enroute action for early flights");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(-1), "dP = totalDelay: deviation absorbed entirely in TMA");
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.Zero;
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: no enroute delay needed");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA delay needed");
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var totalDelay = TimeSpan.FromMinutes(3); // totalDelay = P
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: delay within pressure window, no enroute action");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = totalDelay: all delay absorbed as TMA pressure");
        }

        [Fact]
        public void DelayBeyondPressure_WithinMaximumTmaPressure_ReturnsResume()
        {
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // totalDelay = P + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: approach-first keeps delay in TMA up to maximum pressure");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1), "dP = totalDelay: TMA absorbs all delay within maximum pressure");
        }

        [Fact]
        public void DelayAtMaximumTmaPressureBoundary_ReturnsResume()
        {
            var totalDelay = TimeSpan.FromMinutes(7); // totalDelay = P + dPmax = 3 + 4
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: delay exactly at maximum TMA pressure, still absorbed in TMA");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = totalDelay = P + dPmax: TMA at maximum pressure boundary");
        }

        [Fact]
        public void DelayBeyondMaximumTmaPressure_WithinMaxLinearEnrouteDelay_ReturnsPathStretching()
        {
            // With ShortcutTimeToGain=0, SpeedReduction range is empty; PathStretching starts immediately after P + dPmax
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // totalDelay = P + dPmax + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: excess beyond maximum TMA pressure assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void DelayAtMaxLinearEnrouteDelayBoundary_ReturnsPathStretching()
        {
            var totalDelay = TimeSpan.FromMinutes(12); // totalDelay = P + dPmax + SC + dCmax = 3 + 4 + 0 + 5
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5), "dC = totalDelay - P - dPmax = 5m: enroute at maximum linear capacity");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void DelayBeyondMaxLinearEnrouteDelay_ReturnsHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(12) + TimeSpan.FromSeconds(1); // totalDelay = P + dPmax + SC + dCmax + 1s
            var r = Compute(totalDelay);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: both TMA and enroute linear capacity exhausted");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoPressureTrajectory_SmallPositiveDelay_ReturnsResume()
        {
            // When PressureWindow=0, positive delay up to MaximumTmaPressure is absorbed in approach
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero, "dC = 0: approach-first absorbs delay in TMA before enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromSeconds(1), "dP = totalDelay: delay within maximum TMA pressure, absorbed entirely in TMA");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondPressure_SkipsResume_ReturnsPathStretching()
        {
            // dPmax = 0: Resume range is empty; goes directly from NoDelay to PathStretching
            var totalDelay = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(1); // totalDelay = P + 1s
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P: no additional TMA capacity (dPmax = 0), excess goes to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA at maximum (P = max pressure)");
        }

        [Fact]
        public void WithEqualPressureAndMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // dPmax = 0: Resume range is empty; PathStretching extends to P + dCmax, then Holding
            var totalDelay = TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(1); // totalDelay = P + dCmax + 1s
            var r = Compute(totalDelay, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay - P: both TMA and enroute linear capacity exhausted");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(3), "dP = P: TMA held at maximum (P = max pressure)");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_SmallPositiveDelay_ReturnsPathStretching()
        {
            // P = 0 and dPmax = 0: any positive delay is immediately assigned to enroute via path stretching
            var totalDelay = TimeSpan.FromSeconds(1);
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay: no TMA capacity, all delay assigned to enroute");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA pressure configured");
        }

        [Fact]
        public void WithNoPressureOrMaxPressure_DelayBeyondEnrouteCapacity_ReturnsHolding()
        {
            // P = 0 and dPmax = 0: PathStretching extends to dCmax, then Holding
            var totalDelay = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1); // totalDelay = dCmax + 1s
            var r = Compute(totalDelay, pressureWindow: TimeSpan.Zero, maximumTmaPressure: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1), "dC = totalDelay: no TMA capacity, all delay in enroute including holding");
            r.TerminalDelay.ShouldBe(TimeSpan.Zero, "dP = 0: no TMA pressure configured");
        }

        [Fact]
        public void WithShortcut_DelayAtSpeedReductionBoundary_ReturnsSpeedReduction()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(9); // totalDelay = P + dPmax + SC = 3 + 4 + 2
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2), "dC = totalDelay - P - dPmax = 2m: excess absorbed via enroute shortcut");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void WithShortcut_DelayJustBeyondSpeedReduction_ReturnsPathStretching()
        {
            var shortCut = TimeSpan.FromMinutes(2);
            var totalDelay = TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(1); // totalDelay = P + dPmax + SC + 1s
            var r = Compute(totalDelay, shortCutTimeToGain: shortCut);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: shortcut exhausted, linear enroute delay required");
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
        }

        [Fact]
        public void WithNoMaxLinearEnrouteDelay_LargeDelay_GoesToHolding()
        {
            var totalDelay = TimeSpan.FromMinutes(7) + TimeSpan.FromSeconds(1); // totalDelay = P + dPmax + 1s
            var r = Compute(totalDelay, maxLinearEnrouteDelay: TimeSpan.Zero);
            // With MaxLinearEnrouteDelay=0, PathStretching and SpeedReduction ranges are empty; goes to Holding
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.TerminalDelay.ShouldBe(TimeSpan.FromMinutes(7), "dP = P + dPmax: TMA held at maximum pressure");
            r.EnrouteDelay.ShouldBe(TimeSpan.FromSeconds(1), "dC = totalDelay - P - dPmax: no linear enroute capacity, excess requires holding");
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
            (r.EnrouteDelay + r.TerminalDelay).ShouldBe(TimeSpan.FromMinutes(totalDelayMinutes), "dC + dP must equal total delay");
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
