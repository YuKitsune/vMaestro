using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using Maestro.Core.Tests.Mocks;
using MediatR;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class FlightUpdatedHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly FlightPosition _position = new(
        new Coordinate(0, 0),
        0,
        VerticalTrack.Maintaining,
        0,
        false);

    [Fact]
    public async Task WhenAFlightIsOutOfRangeOfFeederFix_ItShouldNotBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(4),
            "RIVET4",
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddHours(3))]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenAFlightIsInRangeOfFeederFix_ItShouldBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Unstable);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtDepartureAirport_ItShouldBeAddedToThePendingList()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var instanceManager = new MockInstanceManager(sequence);

        var position = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sequence, clock, instanceManager: instanceManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var flight = instance.Session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsUncoupledAtDepartureAirport_ItShouldBeToThePendingList()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var instanceManager = new MockInstanceManager(sequence);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sequence, clock, instanceManager: instanceManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        var flight = instance.Session.PendingFlights.ShouldHaveSingleItem();
        flight.Callsign.ShouldBe(notification.Callsign);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtNonDepartureAirport_ItShouldNotBeTracked()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var position = new FlightPosition(
            new Coordinate(0, 0),
            0,
            VerticalTrack.Maintaining,
            0,
            true);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YXXX", // Non-departure airport
            "YSSY",
            clock.UtcNow().AddHours(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        sequence.Flights.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(State.Unstable)]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAnExistingFlightIsUpdated_ItsEstimatesAreRecalculated(State state)
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();
        sequence.Insert(0, flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(sequence, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldBe(newLandingTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_ButNoPositionIsAvailable_EstimatesAreNotRecalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var originalFeederFixTime = clock.UtcNow().AddMinutes(10);
        var originalLandingTime = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(originalFeederFixTime)
            .WithLandingEstimate(originalLandingTime)
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .Build();
        sequence.Insert(0, flight);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddHours(1)),
                new FixEstimate("YSSY", clock.UtcNow().AddHours(1.25))
            ]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(originalFeederFixTime);
        flight.LandingEstimate.ShouldBe(originalLandingTime);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AndTheFeederFixEstimateWasManuallyAssigned_EstimatesAreNotUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var manualFeederFixEstimate = clock.UtcNow().AddMinutes(10);
        var manualLandingEstimate = clock.UtcNow().AddMinutes(20);
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(manualFeederFixEstimate, manual: true)
            .WithLandingEstimate(manualLandingEstimate)
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .Build();
        sequence.Insert(0, flight);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(15)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(25))
            ]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(manualFeederFixEstimate);
        flight.LandingEstimate.ShouldBe(manualLandingEstimate);
    }

    [Fact]
    public async Task WhenAnExistingFlightIsUpdated_AllFlightDataIsUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .Build();
        sequence.Insert(0, flight);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B744", // Different aircraft type
            AircraftCategory.Jet,
            WakeCategory.Heavy, // Different wake category
            "YMAV", // Different origin
            "YSSY",
            clock.UtcNow().AddHours(-1.5), // Different estimated departure
            TimeSpan.FromHours(2),
            "ODALE7", // Different arrival
            new FlightPosition(new Coordinate(1, 1), 38_000, VerticalTrack.Descending, 280, false), // Different position
            [new FixEstimate("WELSH", clock.UtcNow().AddMinutes(10))]);

        var handler = GetHandler(sequence, clock);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.AircraftType.ShouldBe("B744");
        flight.WakeCategory.ShouldBe(WakeCategory.Heavy);
        flight.OriginIdentifier.ShouldBe("YMAV");
        flight.EstimatedDepartureTime.ShouldBe(notification.EstimatedDepartureTime);
        flight.AssignedArrivalIdentifier.ShouldBe("ODALE7");
        flight.Position!.Coordinate.Latitude.ShouldBe(notification.Position!.Coordinate.Latitude);
        flight.Position.Coordinate.Longitude.ShouldBe(notification.Position.Coordinate.Longitude);
        flight.Position.Altitude.ShouldBe(notification.Position.Altitude);
        flight.Position.GroundSpeed.ShouldBe(notification.Position.GroundSpeed);
        flight.Fixes.ShouldHaveSingleItem().ShouldBe(notification.Estimates.Single());
        flight.LastSeen.ShouldBe(clock.UtcNow());
    }

    [Fact]
    public async Task WhenADesequencedFlightIsUpdated_ItsEstimatesAreStillUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var flight = new FlightBuilder("QFA123")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var instanceManager = new MockInstanceManager(sequence);

        var instance = await instanceManager.GetInstance(sequence.AirportIdentifier, CancellationToken.None);
        instance.Session.DeSequencedFlights.Add(flight);

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(sequence, clock, estimateProvider: estimateProvider, instanceManager: instanceManager);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixEstimate.ShouldBe(newFeederFixTime);
        flight.LandingEstimate.ShouldBe(newLandingTime);
    }

    [Fact]
    public async Task WhenAnUnstableFlightIsUpdated_ItsPositionInSequenceIsRecalculated()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(State.Unstable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Stable)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();
        sequence.Insert(0, flight2); // QFA456 first (earlier estimate)
        sequence.Insert(1, flight1); // QFA123 second (later estimate)

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second initially");

        // Update QFA123 with an earlier landing estimate
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        var newLandingTime = clock.UtcNow().AddMinutes(15);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(sequence, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - QFA123 should now be first due to earlier estimate
        sequence.NumberInSequence(flight1).ShouldBe(1, "QFA123 should be first after update with earlier estimate");
        sequence.NumberInSequence(flight2).ShouldBe(2, "QFA456 should be second after QFA123 moves ahead");
        flight1.LandingEstimate.ShouldBe(newLandingTime, "QFA123 estimate should be updated");
    }

    [Theory]
    [InlineData(State.Stable)]
    [InlineData(State.SuperStable)]
    [InlineData(State.Frozen)]
    public async Task WhenAStableFlightIsUpdated_ItsPositionInSequenceIsNotRecalculated(State state)
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight1 = new FlightBuilder("QFA123")
            .WithState(state)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(20))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(30))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA456")
            .WithState(State.Frozen)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(clock.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();
        sequence.Insert(0, flight2); // QFA456 first (earlier estimate)
        sequence.Insert(1, flight1); // QFA123 second (later estimate)

        // Verify initial order
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should be first initially");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should be second initially");

        // Update QFA123 with an earlier landing estimate (this would normally move it ahead)
        var newFeederFixTime = clock.UtcNow().AddMinutes(5);
        var newLandingTime = clock.UtcNow().AddMinutes(15);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            AircraftCategory.Jet,
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1),
            "RIVET4",
            _position,
            [
                new FixEstimate("RIVET", newFeederFixTime),
                new FixEstimate("YSSY", newLandingTime)
            ]);

        var estimateProvider = Substitute.For<IEstimateProvider>();
        estimateProvider.GetFeederFixEstimate(
                Arg.Any<AirportConfiguration>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<FlightPosition>())
            .Returns(newFeederFixTime);
        estimateProvider.GetLandingEstimate(
                Arg.Any<Flight>(),
                Arg.Any<DateTimeOffset?>())
            .Returns(newLandingTime);

        var handler = GetHandler(sequence, clock, estimateProvider: estimateProvider);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert - Position should remain unchanged for stable flights
        sequence.NumberInSequence(flight2).ShouldBe(1, "QFA456 should remain first - stable flights don't get repositioned");
        sequence.NumberInSequence(flight1).ShouldBe(2, "QFA123 should remain second - stable flights don't get repositioned");

        // But estimates should still be updated
        if (state != State.Landed)
        {
            flight1.LandingEstimate.ShouldBe(newLandingTime, "estimates should still be updated for non-landed flights");
            flight1.FeederFixEstimate.ShouldBe(newFeederFixTime, "estimates should still be updated for non-landed flights");
        }
    }

    FlightUpdatedHandler GetHandler(
        Sequence sequence,
        IClock clock,
        IEstimateProvider? estimateProvider = null,
        IMaestroInstanceManager? instanceManager = null)
    {
        instanceManager ??= new MockInstanceManager(sequence);

        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdateFlight(Arg.Any<Flight>()).Returns(true);

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        estimateProvider ??= Substitute.For<IEstimateProvider>();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            rateLimiter,
            airportConfigurationProvider,
            estimateProvider,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
