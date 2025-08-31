using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
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
    public async Task WhenAFlightIsInRangeOfFeederFix_ItShouldBeSequenced()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            "RIVET4",
            _position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetHandler(sequence, clock, scheduler);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        // Flight is created with New state before being passed to scheduler
        // The real scheduler will set the state to Stable immediately after scheduling
        flight.State.ShouldBe(State.New);
        scheduler.Received(1).Schedule(sequence);
    }

    [Fact]
    public async Task WhenAFlightIsNotTrackingViaFeederFix_ItShouldBeHighPriority()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();
        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            TimeSpan.FromHours(1.5),
            null,
            _position,
            [new FixEstimate("TESAT", clock.UtcNow().AddMinutes(30))]);

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetHandler(sequence, clock, scheduler);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.New);
        flight.HighPriority.ShouldBe(true);
        scheduler.Received(1).Schedule(sequence);
    }

    [Fact]
    public async Task WhenAFlightIsOnGroundAtDepartureAirport_ItShouldBeAddedWithPendingState()
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
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            position,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetHandler(sequence, clock, scheduler);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Pending);
    }

    [Fact]
    public async Task WhenAFlightIsUncoupledAtDepartureAirport_ItShouldBeAddedWithPendingState()
    {
        // Arrange
        var clock = clockFixture.Instance;
        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance).Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
            WakeCategory.Medium,
            "YSCB", // Departure airport configured in fixture
            "YSSY",
            clock.UtcNow().AddMinutes(10),
            TimeSpan.FromHours(20),
            "RIVET4",
            null,
            [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(30))]);

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetHandler(sequence, clock, scheduler);

        // Act
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        var flight = sequence.Flights.ShouldHaveSingleItem();
        flight.State.ShouldBe(State.Pending);
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

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
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
        flight.EstimatedFeederFixTime.ShouldBe(newFeederFixTime);
        flight.EstimatedLandingTime.ShouldBe(newLandingTime);
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
            .WithFlight(flight)
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
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
        flight.EstimatedFeederFixTime.ShouldBe(originalFeederFixTime);
        flight.EstimatedLandingTime.ShouldBe(originalLandingTime);
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
            .WithFlight(flight)
            .Build();

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B744", // Different aircraft type
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
            .WithState(State.Desequenced)
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
            .WithLandingTime(clock.UtcNow().AddMinutes(20))
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var newFeederFixTime = clock.UtcNow().AddMinutes(15);
        var newLandingTime = clock.UtcNow().AddMinutes(25);

        var notification = new FlightUpdatedNotification(
            "QFA123",
            "B738",
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
        flight.EstimatedFeederFixTime.ShouldBe(newFeederFixTime);
        flight.EstimatedLandingTime.ShouldBe(newLandingTime);
    }

    FlightUpdatedHandler GetHandler(
        Sequence sequence,
        IClock clock,
        IScheduler? scheduler = null,
        IEstimateProvider? estimateProvider = null)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.CanSequenceFor(Arg.Is("YSSY")).Returns(true);
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));

        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        rateLimiter.ShouldUpdateFlight(Arg.Any<Flight>(), Arg.Any<FlightPosition>()).Returns(true);

        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        estimateProvider ??= Substitute.For<IEstimateProvider>();
        scheduler ??= Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            sequenceProvider,
            rateLimiter,
            airportConfigurationProvider,
            estimateProvider,
            scheduler,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
