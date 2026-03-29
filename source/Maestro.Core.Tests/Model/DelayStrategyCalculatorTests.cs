using Maestro.Contracts.Shared;
using Maestro.Core.Model;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class DelayStrategyCalculatorTests
{
    static readonly TimeSpan P = TimeSpan.FromMinutes(3);       // approach pressure window
    static readonly TimeSpan DPmax = TimeSpan.FromMinutes(4);   // max pressure window
    static readonly TimeSpan SC = TimeSpan.Zero;                // shortcut (not yet simulated)
    static readonly TimeSpan DCmax = TimeSpan.FromMinutes(5);   // max enroute delay

    public class EnrouteFirst
    {
        [Fact]
        public void EarlyFlight_ReturnsExpedite()
        {
            var dT = TimeSpan.FromMinutes(-1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var dT = TimeSpan.Zero;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var dT = P;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void DelayJustBeyondPressure_ReturnsSpeedReduction()
        {
            var dT = P + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(dT - P);
            r.TmaDelay.ShouldBe(P);
        }

        [Fact]
        public void DelayAtDCmaxBoundary_ReturnsSpeedReduction()
        {
            var dT = P + SC + DCmax;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(dT - P);
            r.TmaDelay.ShouldBe(P);
        }

        [Fact]
        public void DelayBeyondDCmax_ReturnsPathStretching()
        {
            var dT = P + SC + DCmax + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(DCmax);
            r.TmaDelay.ShouldBe(dT - DCmax);
        }

        [Fact]
        public void DelayAtMaxPressureBoundary_ReturnsPathStretching()
        {
            var dT = P + SC + DCmax + DPmax;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(DCmax);
            r.TmaDelay.ShouldBe(dT - DCmax);
        }

        [Fact]
        public void DelayBeyondMaxPressure_ReturnsHolding()
        {
            var dT = P + SC + DCmax + DPmax + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(dT - P - DPmax);
            r.TmaDelay.ShouldBe(P + DPmax);
        }

        [Fact]
        public void WithNoPressureTrajectory_SmallPositiveDelay_ReturnsSpeedReduction()
        {
            // When P=0 and dPmax=0, any positive delay is immediately enroute
            var dT = TimeSpan.FromSeconds(1);
            var r = Compute(dT, p: TimeSpan.Zero, dPmax: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.SpeedReduction);
            r.EnrouteDelay.ShouldBe(dT);
            r.TmaDelay.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public void DelaySplitSumsToTotal()
        {
            var dT = TimeSpan.FromMinutes(7);
            var r = Compute(dT);
            (r.EnrouteDelay + r.TmaDelay).ShouldBe(dT);
        }

        static DelayDistribution Compute(TimeSpan dT, TimeSpan? p = null, TimeSpan? dPmax = null)
        {
            var effectiveP = p ?? P;
            var effectiveDPmax = dPmax ?? DPmax;
            var terminalTrajectory = new TerminalTrajectory(TimeSpan.Zero, effectiveP, effectiveP + effectiveDPmax);
            var enrouteTrajectory = new EnrouteTrajectory(DCmax, SC);
            return DelayStrategyCalculator.Compute(dT, terminalTrajectory, enrouteTrajectory, DelayStrategy.EnrouteFirst);
        }
    }

    public class ApproachFirst
    {
        [Fact]
        public void EarlyFlight_ReturnsExpedite()
        {
            var dT = TimeSpan.FromMinutes(-1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Expedite);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void ZeroDelay_ReturnsNoDelay()
        {
            var dT = TimeSpan.Zero;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public void DelayWithinPressure_ReturnsNoDelay()
        {
            var dT = P;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.NoDelay);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void DelayBeyondPressure_WithinMaxPressure_ReturnsResume()
        {
            var dT = P + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void DelayAtMaxPressureBoundary_ReturnsResume()
        {
            var dT = P + DPmax;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void DelayBeyondMaxPressure_WithinDCmax_ReturnsPathStretching()
        {
            // With SC=0, SpeedReduction range is empty; PathStretching starts immediately after P + dPmax
            var dT = P + DPmax + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(dT - P - DPmax);
            r.TmaDelay.ShouldBe(P + DPmax);
        }

        [Fact]
        public void DelayAtDCmaxBoundary_ReturnsPathStretching()
        {
            var dT = P + DPmax + SC + DCmax;
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.PathStretching);
            r.EnrouteDelay.ShouldBe(dT - P - DPmax);
            r.TmaDelay.ShouldBe(P + DPmax);
        }

        [Fact]
        public void DelayBeyondDCmax_ReturnsHolding()
        {
            var dT = P + DPmax + SC + DCmax + TimeSpan.FromSeconds(1);
            var r = Compute(dT);
            r.ControlAction.ShouldBe(ControlAction.Holding);
            r.EnrouteDelay.ShouldBe(dT - P - DPmax);
            r.TmaDelay.ShouldBe(P + DPmax);
        }

        [Fact]
        public void WithNoPressureTrajectory_SmallPositiveDelay_ReturnsResume()
        {
            // When P=0, positive delay up to dPmax is absorbed in approach
            var dT = TimeSpan.FromSeconds(1);
            var r = Compute(dT, p: TimeSpan.Zero);
            r.ControlAction.ShouldBe(ControlAction.Resume);
            r.EnrouteDelay.ShouldBe(TimeSpan.Zero);
            r.TmaDelay.ShouldBe(dT);
        }

        [Fact]
        public void DelaySplitSumsToTotal()
        {
            var dT = TimeSpan.FromMinutes(10);
            var r = Compute(dT);
            (r.EnrouteDelay + r.TmaDelay).ShouldBe(dT);
        }

        static DelayDistribution Compute(TimeSpan dT, TimeSpan? p = null, TimeSpan? dPmax = null)
        {
            var effectiveP = p ?? P;
            var effectiveDPmax = dPmax ?? DPmax;
            var terminalTrajectory = new TerminalTrajectory(TimeSpan.Zero, effectiveP, effectiveP + effectiveDPmax);
            var enrouteTrajectory = new EnrouteTrajectory(DCmax, SC);
            return DelayStrategyCalculator.Compute(dT, terminalTrajectory, enrouteTrajectory, DelayStrategy.ApproachFirst);
        }
    }
}
