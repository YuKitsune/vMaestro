using Maestro.Core.Handlers;
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

public class ResumeSequencingRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task FlightIsInsertedIntoTheSequenceAndRemovedFromTheDeSequencedList()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, session, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.DeSequencedFlights.Add(flight);

        session.DeSequencedFlights.Count.ShouldBe(1, "Flight should be in desequenced list");
        sequence.Flights.Count.ShouldBe(0, "No flights in sequence initially");

        var mediator = Substitute.For<IMediator>();

        var handler = new ResumeSequencingRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ResumeSequencingRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(1, "Flight should be added to sequence");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "Flight in sequence should be QFA1");
        session.DeSequencedFlights.Count.ShouldBe(0, "Flight should be removed from desequenced list");
    }

    [Fact]
    public async Task FlightIsInsertedByFeederFixEstimate()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        // Use different TTG values to prove positioning is based on FeederFixEstimate when resuming
        // flight1: FF=+5, TTG=15, Landing=+20
        // flight2: FF=+15, TTG=10, Landing=+25
        // flight3: FF=+10, TTG=8, Landing=+18
        // ResumeSequencing positions by FF: flight1 (+5), flight3 (+10), flight2 (+15)
        // Even though flight3 has the earliest landing estimate (+18), it's positioned by FF (+10)
        var flight1 = new FlightBuilder("QFA1")
            .WithFeederFixEstimate(now.AddMinutes(5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFixEstimate(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var trajectoryService = new MockTrajectoryService()
            .WithTrajectoryForFlight(flight1, new Trajectory(TimeSpan.FromMinutes(15)))
            .WithTrajectoryForFlight(flight2, new Trajectory(TimeSpan.FromMinutes(10)))
            .WithTrajectoryForFlight(flight3, new Trajectory(TimeSpan.FromMinutes(8)));

        var (instanceManager, instance, session, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithTrajectoryService(trajectoryService)
                .WithFlightsInOrder(flight1, flight2)
                .WithClock(clockFixture.Instance))
            .Build();

        instance.Session.DeSequencedFlights.Add(flight3);

        var mediator = Substitute.For<IMediator>();

        var handler = new ResumeSequencingRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ResumeSequencingRequest("YSSY", "QFA3");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // Flight inserted based on FeederFixEstimate (ResumeSequencing uses FF for positioning)
        sequence.Flights.Count.ShouldBe(3, "Three flights should be in sequence");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1 (FF=+5)");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3 (FF=+10)");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2 (FF=+15)");

        // Verify landing estimates to prove FF-based positioning
        flight3.LandingEstimate.ShouldBe(now.AddMinutes(18), "QFA3 landing estimate should be FF + TTG = 10 + 8");
        flight1.LandingEstimate.ShouldBe(now.AddMinutes(20), "QFA1 landing estimate should be FF + TTG = 5 + 15");
        flight2.LandingEstimate.ShouldBe(now.AddMinutes(25), "QFA2 landing estimate should be FF + TTG = 15 + 10");

        // Prove positioning is by FF estimate, not landing estimate
        flight3.LandingEstimate.ShouldBeLessThan(flight1.LandingEstimate, "QFA3 lands earlier than QFA1, but positioned later due to FF estimate");
    }

    [Fact]
    public async Task WhenLandingEstimateIsBetweenTwoFrozenFlightsWithInsufficientSpace_ItIsMovedBackUntilThereIsSpace()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        // Create two frozen flights 5 minutes apart
        var frozenFlight1 = new FlightBuilder("QFA1")
            .WithState(State.Frozen)
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var frozenFlight2 = new FlightBuilder("QFA2")
            .WithState(State.Frozen)
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        // Create a third flight with an ETA between the two frozen flights
        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, session, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
                .WithFlightsInOrder(frozenFlight1, frozenFlight2)
                .WithClock(clockFixture.Instance))
            .Build();

        instance.Session.DeSequencedFlights.Add(flight3);

        var originalFrozenFlight1LandingTime = frozenFlight1.LandingTime;
        var originalFrozenFlight2LandingTime = frozenFlight2.LandingTime;

        var mediator = Substitute.For<IMediator>();

        var handler = new ResumeSequencingRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ResumeSequencingRequest("YSSY", "QFA3");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Flights.Count.ShouldBe(3, "Three flights should be in sequence");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA2", "Second flight should be QFA2");
        sequence.Flights[2].Callsign.ShouldBe("QFA3", "Third flight should be QFA3 (placed after frozen flights)");

        // Frozen flight landing times should not be changed
        frozenFlight1.LandingTime.ShouldBe(originalFrozenFlight1LandingTime, "QFA1 landing time should not change");
        frozenFlight2.LandingTime.ShouldBe(originalFrozenFlight2LandingTime, "QFA2 landing time should not change");

        // QFA3 should be placed after the second frozen flight with proper separation
        var acceptanceRate = airportConfigurationFixture.AcceptanceRate;
        flight3.LandingTime.ShouldBe(frozenFlight2.LandingTime.Add(acceptanceRate), "QFA3 should be placed after QFA2 with proper separation");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, session, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance))
            .Build();

        instance.Session.DeSequencedFlights.Add(flight);

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new ResumeSequencingRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new ResumeSequencingRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Flights.Count.ShouldBe(0, "Flight should not be added to sequence locally when relaying to master");
        session.DeSequencedFlights.Count.ShouldBe(1, "Flight should remain in desequenced list when relaying to master");
    }
}
