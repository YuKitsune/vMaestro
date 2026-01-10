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

public class ChangeApproachTypeRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenChangingApproachType_TheApproachTypeIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType("A")
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var arrivalLookup = GetArrivalLookup();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            arrivalLookup,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        flight.ApproachType.ShouldBe("A", "Initial approach type should be A");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe("P", "Approach type should be changed to P");
    }

    [Fact]
    public async Task WhenChangingApproachType_TheLandingEstimateIsUpdated()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var feederFixEstimate = now.AddMinutes(10);
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(feederFixEstimate)
            .WithApproachType("A")
            .WithLandingEstimate(feederFixEstimate.AddMinutes(22))
            .WithLandingTime(feederFixEstimate.AddMinutes(22))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();
        var arrivalLookup = GetArrivalLookup();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            arrivalLookup,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        flight.ApproachType.ShouldBe("A", "Initial approach type should be A");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(22), "Initial landing estimate should be FF + 22 minutes (A approach)");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.ApproachType.ShouldBe("P", "Approach type should be changed to P");
        flight.LandingEstimate.ShouldBe(feederFixEstimate.AddMinutes(25), "Landing estimate should be updated to FF + 25 minutes (P approach)");
    }

    [Fact]
    public async Task RelaysToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("BOREE")
            .WithFeederFixEstimate(now.AddMinutes(10))
            .WithApproachType("A")
            .WithLandingEstimate(now.AddMinutes(32))
            .WithLandingTime(now.AddMinutes(32))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();
        var arrivalLookup = GetArrivalLookup();

        var handler = new ChangeApproachTypeRequestHandler(
            instanceManager,
            slaveConnectionManager,
            arrivalLookup,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeApproachTypeRequest("YSSY", "QFA1", "P");

        var originalApproachType = flight.ApproachType;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight.ApproachType.ShouldBe(originalApproachType, "Local flight should not be modified when relaying to master");
    }

    IArrivalLookup GetArrivalLookup()
    {
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        return new ArrivalLookup(airportConfigurationProvider, Substitute.For<ILogger>());
    }
}
