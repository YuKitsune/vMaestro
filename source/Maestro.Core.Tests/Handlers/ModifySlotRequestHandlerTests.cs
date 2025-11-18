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

public class ModifySlotRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task TheSlotIsModified()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => { })
            .Build();

        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            ["34L"]);

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifySlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var newStartTime = now.AddMinutes(12);
        var newEndTime = now.AddMinutes(18);

        var request = new ModifySlotRequest(
            "YSSY",
            slotId,
            newStartTime,
            newEndTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Slots.Count.ShouldBe(1, "Slot count should remain 1");
        var modifiedSlot = sequence.Slots[0];
        modifiedSlot.Id.ShouldBe(slotId, "Slot ID should remain unchanged");
        modifiedSlot.StartTime.ShouldBe(newStartTime, "Slot start time should be updated");
        modifiedSlot.EndTime.ShouldBe(newEndTime, "Slot end time should be updated");
    }

    [Fact(Skip = "Expected behviour is unclear")]
    public async Task TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(12))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Create slot from T10 to T15, which covers flight1's estimate (T12) but not flight2's (T18)
        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            ["34L"]);

        // Flight1 should have been delayed to after the slot
        flight1.LandingTime.ShouldBe(now.AddMinutes(15));

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifySlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        // Modify slot to T15-T20, now flight1's estimate (T12) is before the slot, and flight2's estimate (T18) is during
        var newStartTime = now.AddMinutes(15);
        var newEndTime = now.AddMinutes(20);

        var request = new ModifySlotRequest(
            "YSSY",
            slotId,
            newStartTime,
            newEndTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        // BUG: This will fail because the slot starts BEFORE the STA, so the flight still gets pushed back.
        //  The flight will need to be re-inserted based on it's landing estimate for it to move in front of the slot.
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "QFA1 estimate is now before the slot, should land at its estimate");
        flight2.LandingTime.ShouldBe(newEndTime, "QFA2 estimate is now during the slot, should land at slot end time");
    }

    [Fact]
    public async Task FrozenFlightsAreUnaffected()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(12))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway("34L")
            .WithState(State.Frozen)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Create slot from T10 to T15, which covers flight1 (T12) but not flight2 (T18)
        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            ["34L"]);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        var mediator = Substitute.For<IMediator>();

        var handler = new ModifySlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        // Modify slot to T15-T20, now it covers flight2 (T18) but not flight1 (T12)
        var newStartTime = now.AddMinutes(15);
        var newEndTime = now.AddMinutes(20);

        var request = new ModifySlotRequest(
            "YSSY",
            slotId,
            newStartTime,
            newEndTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 should remain unchanged");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "Frozen flight QFA2 should remain unchanged");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => { })
            .Build();

        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            ["34L"]);

        var originalStartTime = sequence.Slots[0].StartTime;
        var originalEndTime = sequence.Slots[0].EndTime;

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new ModifySlotRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var newStartTime = now.AddMinutes(12);
        var newEndTime = now.AddMinutes(18);

        var request = new ModifySlotRequest(
            "YSSY",
            slotId,
            newStartTime,
            newEndTime);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");

        sequence.Slots[0].StartTime.ShouldBe(originalStartTime, "Slot should not be modified locally when relaying to master");
        sequence.Slots[0].EndTime.ShouldBe(originalEndTime, "Slot should not be modified locally when relaying to master");
    }
}
