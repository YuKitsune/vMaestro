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
    public async Task FlightIsInsertedByLandingEstimate()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, session, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s
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
        sequence.Flights.Count.ShouldBe(3, "Three flights should be in sequence");
        sequence.Flights[0].Callsign.ShouldBe("QFA1", "First flight should be QFA1");
        sequence.Flights[1].Callsign.ShouldBe("QFA3", "Second flight should be QFA3");
        sequence.Flights[2].Callsign.ShouldBe("QFA2", "Third flight should be QFA2");

        // Assert that each flight's landing time is separated by the acceptance rate
        var acceptanceRate = airportConfigurationFixture.AcceptanceRate;
        flight3.LandingTime.ShouldBe(flight1.LandingTime.Add(acceptanceRate), "QFA3 should be delayed behind QFA1 by acceptance rate");
        flight2.LandingTime.ShouldBe(flight3.LandingTime.Add(acceptanceRate), "QFA2 should be delayed behind QFA3 by acceptance rate");
    }

    [Fact]
    public async Task WhenEstimateIsBetweenTwoFrozenFlightsWithInsufficientSpace_ItIsMovedBackUntilThereIsSpace()
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
