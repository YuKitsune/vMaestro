using Maestro.Core.Configuration;
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

public class DeleteSlotRequestHandlerTests(ClockFixture clockFixture)
{
    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task TheSlotIsDeleted()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => { })
            .Build();

        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            [DefaultRunway]);

        sequence.Slots.Count.ShouldBe(1, "Slot should be created");

        var mediator = Substitute.For<IMediator>();

        var handler = new DeleteSlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new DeleteSlotRequest("YSSY", slotId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.Slots.Count.ShouldBe(0, "Slot should be deleted");
    }

    [Fact]
    public async Task TheSequenceIsRecalculated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(12))
            .WithLandingTime(now.AddMinutes(12))
            .WithRunway(DefaultRunway)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(18))
            .WithLandingTime(now.AddMinutes(18))
            .WithRunway(DefaultRunway)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Create slot from T10 to T15, which covers flight1's estimate (T12)
        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            [DefaultRunway]);

        // Flight1 should have been delayed to after the slot
        flight1.LandingTime.ShouldBe(now.AddMinutes(15), "QFA1 should be delayed to slot end time");
        flight2.LandingTime.ShouldBe(now.AddMinutes(18), "QFA2 should maintain proper separation");

        var mediator = Substitute.For<IMediator>();

        var handler = new DeleteSlotRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            Substitute.For<ILogger>());

        var request = new DeleteSlotRequest("YSSY", slotId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(flight1.LandingEstimate, "QFA1 should land at its estimate after slot is deleted");
        flight2.LandingTime.ShouldBe(flight2.LandingEstimate, "QFA2 should land at its estimate after slot is deleted");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var airportConfiguration = CreateAirportConfiguration();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => { })
            .Build();

        var slotId = sequence.CreateSlot(
            now.AddMinutes(10),
            now.AddMinutes(15),
            [DefaultRunway]);

        sequence.Slots.Count.ShouldBe(1, "Slot should be created");

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new DeleteSlotRequestHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            Substitute.For<ILogger>());

        var request = new DeleteSlotRequest("YSSY", slotId);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.Slots.Count.ShouldBe(1, "Slot should not be deleted locally when relaying to master");
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways(DefaultRunway)
            .WithRunwayMode("DEFAULT", new RunwayConfiguration
            {
                Identifier = DefaultRunway,
                LandingRateSeconds = DefaultLandingRateSeconds,
                FeederFixes = []
            })
            .Build();
    }
}
