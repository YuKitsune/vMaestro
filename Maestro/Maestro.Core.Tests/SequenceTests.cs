using Maestro.Core.Configuration;
using Maestro.Core.Dtos;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Tests.Fixtures;
using MediatR;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests;

public class SequenceTests(AirportConfigurationFixture airportConfigurationFixture)
{
    public void WhenAFlightIsAdded_ARunwayIsAssigned()
    {
        Assert.Fail("Stub");
    }

    public void WhenAFlightIsAdded_AFeederFixIsAssigned()
    {
        Assert.Fail("Stub");
    }

    public void WhenAFlightIsAdded_EstimatesAreCalculated()
    {
        Assert.Fail("Stub");
    }
    
    // [Fact]
    // public async Task WhenFlightsAreAdded_TheyAreSequenced()
    // {
    //     // Arrange
    //     var clock = new FixedClock(DateTimeOffset.Now);
    //     var landingRate = TimeSpan.FromMinutes(2);
    //     var sequence = CreateSequence(Substitute.For<IMediator>(), clock, landingRate);
    //
    //     var flight1 = new Flight
    //     {
    //         Callsign = "QFA1",
    //         AircraftType = "B738",
    //         WakeCategory = WakeCategory.Medium,
    //         OriginIdentifier = "YMML",
    //         DestinationIdentifier = "YSSY",
    //         FeederFixIdentifier = "RIVET"
    //     };
    //     flight1.UpdatePosition(
    //         new FlightPosition(
    //             new Coordinate(10, 10),
    //             34_000,
    //             VerticalTrack.Maintaining,
    //             450),
    //         [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(1))],
    //         clock);
    //
    //     var flight2 = new Flight
    //     {
    //         Callsign = "QFA2",
    //         AircraftType = "B738",
    //         WakeCategory = WakeCategory.Medium,
    //         OriginIdentifier = "YMML",
    //         DestinationIdentifier = "YSSY",
    //         FeederFixIdentifier = "RIVET"
    //     };
    //     flight2.UpdatePosition(
    //         new FlightPosition(
    //             new Coordinate(12, 12),
    //             34_000,
    //             VerticalTrack.Maintaining,
    //             450),
    //         [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(2))],
    //         clock);
    //
    //     var flight3 = new Flight
    //     {
    //         Callsign = "QFA3",
    //         AircraftType = "B738",
    //         WakeCategory = WakeCategory.Medium,
    //         OriginIdentifier = "YMML",
    //         DestinationIdentifier = "YSSY",
    //         FeederFixIdentifier = "RIVET"
    //     };
    //     flight3.UpdatePosition(
    //         new FlightPosition(
    //             new Coordinate(12, 12),
    //             34_000,
    //             VerticalTrack.Maintaining,
    //             450),
    //         [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(4))],
    //         clock);
    //     
    //     // Act
    //     await sequence.Add(flight1, CancellationToken.None);
    //     await sequence.Add(flight2, CancellationToken.None);
    //     await sequence.Add(flight3, CancellationToken.None);
    //     var result = sequence.ComputeSequence(sequence.Flights);
    //
    //     // Assert
    //     var first = result[0];
    //     first.Callsign.ShouldBe("QFA1");
    //     first.TotalDelayToRunway.ShouldBe(TimeSpan.Zero);
    //     first.TotalDelayToFeederFix.ShouldBe(TimeSpan.Zero);
    //         
    //     var second = result[1];
    //     second.Callsign.ShouldBe("QFA2");
    //     second.ScheduledLandingTime.ShouldBe(first.ScheduledLandingTime + landingRate);
    // }
    
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

    // Sequence CreateSequence(IMediator mediator, IClock clock, TimeSpan landingRate)
    // {
    //     var landingRateProvider = Substitute.For<ISeparationRuleProvider>();
    //     landingRateProvider.GetRequiredSpacing(Arg.Any<Flight>(), Arg.Any<Flight>(), Arg.Any<RunwayModeConfiguration>()).Returns(landingRate);
    //     
    //     var estimateProvider = Substitute.For<IEstimateProvider>();
    //     
    //     return new Sequence(
    //         airportConfigurationFixture.Instance,
    //         landingRateProvider,
    //         mediator,
    //         clock,
    //         estimateProvider);
    // }
}