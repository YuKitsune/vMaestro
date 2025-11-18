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

public class ChangeRunwayModeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenStartTimeIsNowOrEarlier_CurrentRunwayModeIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial runway mode
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA");

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            });

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("16IVA");
        sequence.NextRunwayMode.ShouldBeNull();
        sequence.LastLandingTimeForCurrentMode.ShouldBeNull();
        sequence.FirstLandingTimeForNewMode.ShouldBeNull();
    }

    [Fact]
    public async Task WhenStartTimeIsInTheFuture_NextRunwayModeAndStartTimesAreSet()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        // Verify initial runway mode
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA");

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            });

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.CurrentRunwayMode.Identifier.ShouldBe("34IVA", "Current mode should remain unchanged");
        sequence.NextRunwayMode.ShouldNotBeNull();
        sequence.NextRunwayMode!.Identifier.ShouldBe("16IVA");
        sequence.LastLandingTimeForCurrentMode.ShouldBe(lastLandingTimeForOldMode);
        sequence.FirstLandingTimeForNewMode.ShouldBe(firstLandingTimeForNewMode);
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_ReAssignedToNewRunways()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(30))
            .WithLandingTime(now.AddMinutes(30))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(33))
            .WithLandingTime(now.AddMinutes(33))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            });

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "QFA1 lands before mode change, should remain on 34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "QFA2 lands before mode change, should remain on 34R");
        flight3.AssignedRunwayIdentifier.ShouldBe("16R", "QFA3 lands after mode change, should be reassigned to 16R");
        flight4.AssignedRunwayIdentifier.ShouldBe("16L", "QFA4 lands after mode change, should be reassigned to 16L");
    }

    [Fact]
    public async Task FlightsScheduledToLandAfterNewMode_AreRescheduled()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(24))
            .WithLandingTime(now.AddMinutes(24))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(27))
            .WithLandingTime(now.AddMinutes(27))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        // New mode with 240-second (4-minute) acceptance rate instead of 180 seconds (3 minutes)
        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 240 },
                { "16R", 240 }
            });

        var lastLandingTimeForOldMode = now.AddMinutes(20);
        var firstLandingTimeForNewMode = now.AddMinutes(25);

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            lastLandingTimeForOldMode,
            firstLandingTimeForNewMode);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "QFA1 lands before mode change, landing time should remain unchanged");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "QFA2 lands before mode change, landing time should remain unchanged");

        flight3.LandingTime.ShouldBeGreaterThanOrEqualTo(firstLandingTimeForNewMode, "QFA3 should be delayed until after the mode change");
        flight4.LandingTime.ShouldBeGreaterThanOrEqualTo(flight3.LandingTime.AddSeconds(240), "QFA4 should maintain 240-second separation from QFA3");

        sequence.Flights[0].Callsign.ShouldBe("QFA1", "Order should remain: QFA1 first");
        sequence.Flights[1].Callsign.ShouldBe("QFA2", "Order should remain: QFA2 second");
        sequence.Flights[2].Callsign.ShouldBe("QFA3", "Order should remain: QFA3 third");
        sequence.Flights[3].Callsign.ShouldBe("QFA4", "Order should remain: QFA4 fourth");
    }

    [Fact]
    public async Task FrozenFlights_AreNotAffected()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithState(State.Frozen)
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(13))
            .WithLandingTime(now.AddMinutes(13))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .WithState(State.Frozen)
            .Build();

        var flight3 = new FlightBuilder("QFA3")
            .WithLandingEstimate(now.AddMinutes(16))
            .WithLandingTime(now.AddMinutes(16))
            .WithRunway("34L")
            .WithFeederFix("RIVET")
            .WithState(State.Unstable)
            .Build();

        var flight4 = new FlightBuilder("QFA4")
            .WithLandingEstimate(now.AddMinutes(19))
            .WithLandingTime(now.AddMinutes(19))
            .WithRunway("34R")
            .WithFeederFix("BOREE")
            .WithState(State.Unstable)
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2, flight3, flight4))
            .Build();

        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            });

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        var originalFlight1LandingTime = flight1.LandingTime;
        var originalFlight2LandingTime = flight2.LandingTime;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "Frozen flight QFA1 should remain on 34L");
        flight1.LandingTime.ShouldBe(originalFlight1LandingTime, "Frozen flight QFA1 landing time should remain unchanged");

        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "Frozen flight QFA2 should remain on 34R");
        flight2.LandingTime.ShouldBe(originalFlight2LandingTime, "Frozen flight QFA2 landing time should remain unchanged");

        flight3.AssignedRunwayIdentifier.ShouldBe("16R", "Non-frozen flight QFA3 should be reassigned to 16R");
        flight4.AssignedRunwayIdentifier.ShouldBe("16L", "Non-frozen flight QFA4 should be reassigned to 16L");
    }

    [Fact]
    public async Task RedirectedToMaster()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, sequence) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithFlightsInOrder(flight1))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var airportConfigurationProvider = new AirportConfigurationProvider([airportConfigurationFixture.Instance]);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayModeRequestHandler(
            instanceManager,
            slaveConnectionManager,
            airportConfigurationProvider,
            clockFixture.Instance,
            mediator,
            Substitute.For<ILogger>());

        var runwayModeDto = new RunwayModeDto(
            "16IVA",
            new Dictionary<string, int>
            {
                { "16L", 180 },
                { "16R", 180 }
            });

        var request = new ChangeRunwayModeRequest(
            "YSSY",
            runwayModeDto,
            now,
            now);

        var originalRunwayMode = sequence.CurrentRunwayMode.Identifier;

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        sequence.CurrentRunwayMode.Identifier.ShouldBe(originalRunwayMode, "Local sequence should not be modified when relaying to master");
    }
}
