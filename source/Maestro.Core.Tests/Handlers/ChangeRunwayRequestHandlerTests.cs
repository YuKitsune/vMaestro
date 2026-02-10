using Maestro.Core.Handlers;
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
            Substitute.For<IArrivalLookup>(),
            Substitute.For<IClock>(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34R", "QFA1 should be assigned to 34R");

        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA1 should be delayed to maintain separation behind QFA2");
        flight1.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));

        // Verify QFA1 is now scheduled on 34R and positioned appropriately
        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R");
        sequence.NumberForRunway(flight1).ShouldBe(2, "QFA1 should be #2 on 34R after moving to 34R");
    }

    [Fact]
    public async Task WhenChangingRunway_TheFlightIsMovedBasedOnItsLandingEstimate()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA2", "34R");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "QFA2 should be assigned to 34R");

        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R (positioned before QFA3 based on earlier estimate)");
        sequence.NumberForRunway(flight3).ShouldBe(2, "QFA3 should be #2 on 34R");
    }

    [Fact]
    public async Task WhenChangingRunway_TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithFeederFix("RIVET")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithFeederFix("BOREE") // TODO: Sequence will re-assign the runway based on feeder fix preferences; need to remove this from the sequencing logic
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(12))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA2", "34R");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "QFA2 should be assigned to 34R");
        flight4.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA4 landing time should be adjusted to maintain separation from QFA2");

        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA3 landing time should be reduced to minimize delay after QFA2 moved to 34R");
    }

    [Fact]
    public async Task WhenChangingRunway_AndTheFlightWasUnstable_ItBecomesStable()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        flight.State.ShouldBe(State.Unstable, "Flight should initially be Unstable");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Stable, "Unstable flight should become Stable when runway is changed");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    [InlineData(State.Landed)]
    public async Task WhenChangingRunway_AndTheFlightWasNotUnstable_ItBecomesStable(State state)
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(state)
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        flight.State.ShouldBe(state, $"Flight should initially be {state}");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(state, $"Flight state should remain {state} when changing runway");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            instanceManager,
            slaveConnectionManager,
            Substitute.For<IArrivalLookup>(),
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        var originalRunway = flight.AssignedRunwayIdentifier;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight.AssignedRunwayIdentifier.ShouldBe(originalRunway, "Local flight should not be modified when relaying to master");
    }
}
