using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightLandedNotificationHandlerTests(ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenFlightNotInSequence_DoesNothing()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var notification = new FlightLandedNotification("YSSY", "QFA999", now);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        await mediator.DidNotReceive().Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenFlightLandedOnOffModeRunway_DoesNotUpdateAchievedRates()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "16R")
            .WithRunwayMode("34L",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("16R") // Off-mode runway
            .WithState(State.Stable) // Stabilize to prevent re-assigning to the in-mode runway
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var notification = new FlightLandedNotification("YSSY", "QFA1", now);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        instance.Session.LandingStatistics.AchievedLandingRates.ShouldBeEmpty();

        await mediator.DidNotReceive().Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenFlightLandsOnActiveRunway_RecordsLandingTimeAndPublishesSessionUpdate()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var landingTime = now;
        var notification = new FlightLandedNotification("YSSY", "QFA1", landingTime);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        instance.Session.LandingStatistics.AchievedLandingRates.ShouldContainKey("34L");

        await mediator.Received(1).Publish(
            Arg.Is<SessionUpdatedNotification>(n =>
                n.AirportIdentifier == "YSSY" &&
                n.Session.LandingStatistics.RunwayLandingTimes.ContainsKey("34L")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenMultipleFlightsLand_RecordsAllLandingTimes()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-12))
            .WithLandingTime(now.AddMinutes(-2))
            .WithRunway("34L")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var notification1 = new FlightLandedNotification("YSSY", "QFA1", now.AddMinutes(-5));
        var notification2 = new FlightLandedNotification("YSSY", "QFA2", now.AddMinutes(-2));

        // Act
        await handler.Handle(notification1, CancellationToken.None);
        await handler.Handle(notification2, CancellationToken.None);

        // Assert
        var snapshot = instance.Session.LandingStatistics.Snapshot();
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.Length.ShouldBe(2);
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes[0].ShouldBe(now.AddMinutes(-5));
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes[1].ShouldBe(now.AddMinutes(-2));

        await mediator.Received(2).Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenFlightsLandOnDifferentRunways_RecordsLandingTimesPerRunway()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] },
                new RunwayConfiguration { Identifier = "34R", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight1 = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34R")
            .Build();

        var (instanceManager, instance, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight1, flight2))
            .Build();

        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var notification1 = new FlightLandedNotification("YSSY", "QFA1", now.AddMinutes(-3));
        var notification2 = new FlightLandedNotification("YSSY", "QFA2", now.AddMinutes(-3));

        // Act
        await handler.Handle(notification1, CancellationToken.None);
        await handler.Handle(notification2, CancellationToken.None);

        // Assert
        var snapshot = instance.Session.LandingStatistics.Snapshot();
        snapshot.RunwayLandingTimes.ShouldContainKey("34L");
        snapshot.RunwayLandingTimes.ShouldContainKey("34R");
        snapshot.RunwayLandingTimes["34L"].ActualLandingTimes.Length.ShouldBe(1);
        snapshot.RunwayLandingTimes["34R"].ActualLandingTimes.Length.ShouldBe(1);
    }

    [Fact]
    public async Task WhenConnectedAsSlave_RelaysNotificationToMaster()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L")
            .WithRunwayMode("34L",
                new RunwayConfiguration { Identifier = "34L", ApproachType = "", LandingRateSeconds = 180, FeederFixes = [] })
            .Build();

        var flight = new FlightBuilder("QFA1")
            .WithTrajectory(new Trajectory(TimeSpan.FromMinutes(10)))
            .WithFeederFixEstimate(now.AddMinutes(-15))
            .WithLandingTime(now.AddMinutes(-5))
            .WithRunway("34L")
            .Build();

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(flight))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new FlightLandedNotificationHandler(
            instanceManager,
            slaveConnectionManager,
            mediator,
            clockFixture.Instance,
            Substitute.For<ILogger>());

        var notification = new FlightLandedNotification("YSSY", "QFA1", now);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedNotifications.Count.ShouldBe(1);
        slaveConnectionManager.Connection.InvokedNotifications[0].ShouldBe(notification);

        // Should not process locally when relaying
        await mediator.DidNotReceive().Publish(
            Arg.Any<SessionUpdatedNotification>(),
            Arg.Any<CancellationToken>());
    }
}
