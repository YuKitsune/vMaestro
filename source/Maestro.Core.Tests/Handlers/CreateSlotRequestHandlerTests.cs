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

public class CreateSlotRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenCreatingSlot_FlightsLandingAfterTheStartTimeAreRescheduled()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new CreateSlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var slotStartTime = now.AddMinutes(12);
        var slotEndTime = now.AddMinutes(14);

        var request = new CreateSlotRequest(
            "YSSY",
            slotStartTime,
            slotEndTime,
            ["34L"]);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "QFA1 lands before slot, should remain unchanged");
        flight2.LandingTime.ShouldBe(slotEndTime, "QFA2 should land exactly at slot end time");
        flight3.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA3 should land exactly one acceptance rate after QFA2");

        sequence.Slots.Count.ShouldBe(1, "One slot should be created");
        sequence.Slots[0].StartTime.ShouldBe(slotStartTime);
        sequence.Slots[0].EndTime.ShouldBe(slotEndTime);
    }

    [Fact]
    public async Task WhenCreatingSlot_FrozenFlightsAreUnaffected()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new CreateSlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var slotStartTime = now.AddMinutes(8);
        var slotEndTime = now.AddMinutes(15);

        var request = new CreateSlotRequest(
            "YSSY",
            slotStartTime,
            slotEndTime,
            ["34L"]);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 should remain unchanged");
        flight2.LandingTime.ShouldBe(slotEndTime, "QFA2 should land exactly at slot end time");
    }

    [Fact]
    public async Task WhenCreatingSlot_SeparationIsMaintainedWithFrozenFlights()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new CreateSlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var slotStartTime = now.AddMinutes(8);
        var slotEndTime = now.AddMinutes(11);

        var request = new CreateSlotRequest(
            "YSSY",
            slotStartTime,
            slotEndTime,
            ["34L"]);

        var originalFlight1LandingTime = flight1.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 should remain unchanged");

        // QFA2 must land after both the slot ends AND maintain separation from the frozen flight
        var expectedFlight2LandingTime = flight1.LandingTime.Add(airportConfigurationFixture.AcceptanceRate);
        flight2.LandingTime.ShouldBe(expectedFlight2LandingTime, "QFA2 should maintain separation from frozen flight QFA1");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new CreateSlotRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var slotStartTime = now.AddMinutes(12);
        var slotEndTime = now.AddMinutes(14);

        var request = new CreateSlotRequest(
            "YSSY",
            slotStartTime,
            slotEndTime,
            ["34L"]);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Slots.Count.ShouldBe(0, "No slots should be created locally when relaying to master");
    }

}
