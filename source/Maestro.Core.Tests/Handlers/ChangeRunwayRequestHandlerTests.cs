using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ChangeRunwayRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenChangingRunway_TheRunwayIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(11))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .WithFeederFix("BOREE") // TODO: Sequence will re-assign the runway based on feeder fix preferences; need to remove this from the sequencing logic
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial runway assignments and ordering
        flight1.AssignedRunwayIdentifier.ShouldBe("34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R");
        sequence.NumberForRunway(flight1).ShouldBe(1, "QFA1 should be #1 on 34L initially");
        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R initially");

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IClock>(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34R", "QFA1 should be assigned to 34R");
        flight1.RunwayManuallyAssigned.ShouldBe(true, "runway should be marked as manually assigned");

        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA1 should be delayed to maintain separation behind QFA2");
        flight1.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));

        // Verify QFA1 is now scheduled on 34R and positioned appropriately
        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R");
        sequence.NumberForRunway(flight1).ShouldBe(2, "QFA1 should be #2 on 34R after moving to 34R");
    }

    [Fact]
    public async Task WhenChangingRunway_TheFlightIsMovedBasedOnItsLandingEstimate()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create three flights.
        // - Two flights should be on 34L with the same landing estimate
        // - One flight should be on 34R with a later landing estimate

        // Act
        // TODO: Re-assign the second flight on 34L to 34R.

        // Assert
        // TODO: Assert that the flight is now on 34R
        // TODO: Assert that the flight is positioned before the existing flight on 34R based on its landing estimate
    }

    [Fact]
    public async Task WhenChangingRunway_TheSequenceIsRecalculated()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create four flights.
        // - Three flights should be on 34L with the same landing estimate
        // - One flight should be on 34R with a later landing estimate

        // Act
        // TODO: Re-assign the second flight on 34L to 34R.

        // Assert
        // TODO: Assert that the flight is now on 34R
        // TODO: Assert that the landing time of the existing flight on 34R is adjusted to maintain proper separation
        // TODO: Assert that the landing times of the remaining flights on 34L are adjusted to minimise the delay
    }

    [Fact]
    public async Task WhenChangingRunway_AndTheFlightWasUnstable_ItBecomesStable()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight that is unstable

        // Act
        // TODO: Change the runway of the flight

        // Assert
        // TODO: Assert that the flight's state is now Stable
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenChangingRunway_AndTheFlightWasNotUnstable_ItBecomesStable(State state)
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a flight with the specified state

        // Act
        // TODO: Change the runway of the flight

        // Assert
        // TODO: Assert that the flight's state remains unchanged
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        await Task.CompletedTask;
        Assert.Fail("Not implemented");

        // Arrange
        // TODO: Create a dummy connection that simulates a non-master instance

        // Act
        // TODO: Change the runway

        // Assert
        // TODO: Assert that the request was redirected to the master and not handled locally
    }
}
