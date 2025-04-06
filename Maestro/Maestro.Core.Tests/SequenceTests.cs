namespace Maestro.Core.Tests;

public class SequenceTests
{
    public void WhenAFlightIsRecomputed_ControllerInterventionsAreCancelled()
    {
        // ZeroDelay removed
        // Custom estimates removed
        // Custom runway removed
        // Custom position removed
        // Update the feeder fix if rerouted
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsRerouted_ToAnotherFeederFix_TheFeederIsNotChanged()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsRerouted_PastTheFeederFix_TheFeederIsNotChanged()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAnUnstableFlightIsUpdated_ItIsRecomputed()
    {
        Assert.Fail("Stub");
    }

    public void WhenAStableFlightIsUpdated_EstimatesAreUpdated()
    {
        Assert.Fail("Stub");
    }

    public void WhenASuperStableFlightIsUpdated_EstimatesAreUpdated()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsWithinTheSpecifiedRange_ItIsStablised()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightHasPassed_OriginalFeederFixEstimate_ItIsSuperStablised()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsWithinTheSpecifiedRange_ItIsFrozen()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightHasPassed_ScheduledLandingTime_ItIsFrozen()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAStableFlight_ChangesFeederFixEstimate_ItIsNotMoved()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenASuperStableFlight_ChangesFeederFixEstimate_ItIsNotMoved()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenANewFlightIsAdded_ItIsRecomputed()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsRecomputed_WithFeederFixEstimate_BeforeStableFlight_StableFlightIsMoved()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsRecomputed_WithFeederFixEstimate_BeforeSuperStableFlight_FlightIsDelayed()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenABlockoutPeriodExists_NoFlightsCanBeSequencedWithinTheBlockoutPeriod()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightIsManuallyInserted_AllFlightsAfterwardsAreDelayed()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightHasLanded_AndTheSpecifiedTimeHasNotPassed_FlightRemains()
    {
        Assert.Fail("Stub");
    }
    
    public void WhenAFlightHasLanded_AndTheSpecifiedTimeHasPassed_FlightIsRemoved()
    {
        Assert.Fail("Stub");
    }

    public void WhenTheRunwayModeWillChange_FlightsWithEstimatesBeforeTheChange_AreSequencedForTheCurrentRunwayMode()
    {
        Assert.Fail("Stub");
    }

    public void WhenTheRunwayModeWillChange_FlightsWithEstimatesAfterTheChange_AreSequencedForTheNextRunwayMode()
    {
        Assert.Fail("Stub");
    }

    public void WhenAssigningARunway_TheSpecifiedRulesAreFollowed()
    {
        Assert.Fail("Stub");
    }
}