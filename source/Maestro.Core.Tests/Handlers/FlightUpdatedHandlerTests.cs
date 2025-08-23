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
            "RIVET4",
            "34L",
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
            "RIVET4",
            "34L",
            null,
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
            null,
            "34L",
            null,
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
            "RIVET4",
            "34L",
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
            "RIVET4",
            "34L",
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
            "RIVET4",
            "34L",
            null,
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

    // TODO
    // [Fact]
    // public async Task WhenAnExistingFlightIsUpdated_AllFlightDataIsUpdated()
    // {
    //
    //     // Arrange
    //     var clock = clockFixture.Instance;
    //     var flight = new FlightBuilder("QFA123")
    //         .WithFeederFix("RIVET")
    //         .WithFeederFixEstimate(clock.UtcNow().AddMinutes(10))
    //         .Build();
    //
    //     var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
    //         .WithFlight(flight)
    //         .Build();
    //
    //     var notification = new FlightUpdatedNotification(
    //         "QFA123",
    //         "B738",
    //         WakeCategory.Medium,
    //         "YMML",
    //         "YSSY",
    //         clock.UtcNow().AddHours(-1),
    //         "ODALE7", // Different arrival
    //         "34R", // Different runway
    //         null,
    //         [new FixEstimate("RIVET", clock.UtcNow().AddMinutes(10))]);
    //
    //     var handler = GetHandler(sequence, clock);
    //
    //     // Act
    //     await handler.Handle(notification, CancellationToken.None);
    //
    //     // Assert
    //     flight.AssignedArrival.ShouldBe("ODALE7");
    //     flight.LastSeen.ShouldBe(clock.UtcNow());
    // }

    // TODO: Move recompute checks elsewhere

    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndItHasBeenRerouted_TheFeederFixIsUpdated()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        // Change the feeder fix (emulate a re-route)
        var newEtaFf = DateTimeOffset.Now.AddMinutes(5);
        var notification = new FlightUpdatedNotification(
            "QFA1",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            "ODALE7",
            "34L",
            null,
            [
                new FixEstimate("AKMIR", newEtaFf),
                new FixEstimate("YSSY", newEtaFf.AddMinutes(10))
            ]);

        var handler = GetHandler(sequence, clock);

        // Act
        flight.NeedsRecompute = true;
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.FeederFixIdentifier.ShouldBe("AKMIR");
        flight.EstimatedFeederFixTime.ShouldBe(newEtaFf);
        flight.NeedsRecompute.ShouldBe(false);
    }

    // TODO: Verify this behavior is desired.
    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndARunwayHasBeenManuallyAssigned_ItIsNotOverridden()
    {
        // Arrange
        var clock = clockFixture.Instance;

        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        // Change the runway
        flight.SetRunway("34R", manual: true);

        var notification = new FlightUpdatedNotification(
            "QFA1",
            "B738",
            WakeCategory.Medium,
            "YMML",
            "YSSY",
            clock.UtcNow().AddHours(-1),
            "RIVET4",
            "34R",
            null,
            [
                new FixEstimate("RIVET", clock.UtcNow().AddMinutes(5)),
                new FixEstimate("YSSY", clock.UtcNow().AddMinutes(10))
            ]);

        var handler = GetHandler(sequence, clock);

        // Act
        flight.NeedsRecompute = true;
        await handler.Handle(notification, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);
        flight.NeedsRecompute.ShouldBe(false);
    }

    [Fact]
    public async Task WhenAFlightNeedsRecomputing_AndACustomFeederFixEstimateWasProvided_ItIsOverridden()
    {
        await Task.CompletedTask;
        Assert.Fail("Stub");
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

        var runwayAssigner = Substitute.For<IRunwayAssigner>();
        var rateLimiter = Substitute.For<IFlightUpdateRateLimiter>();
        var airportConfigurationProvider = Substitute.For<IAirportConfigurationProvider>();
        airportConfigurationProvider.GetAirportConfigurations().Returns([airportConfigurationFixture.Instance]);

        estimateProvider ??= Substitute.For<IEstimateProvider>();
        scheduler ??= Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();

        return new FlightUpdatedHandler(
            sequenceProvider,
            runwayAssigner,
            rateLimiter,
            airportConfigurationProvider,
            estimateProvider,
            scheduler,
            mediator,
            clock,
            Substitute.For<ILogger>());
    }
}
