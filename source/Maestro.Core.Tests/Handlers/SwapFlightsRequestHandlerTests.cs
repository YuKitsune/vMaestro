using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
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

public class SwapFlightsRequestHandlerTests(ClockFixture clockFixture)
{
    readonly FixedClock _clock = clockFixture.Instance;

    const string DefaultRunway = "34L";
    const int DefaultLandingRateSeconds = 180;

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirPositionsAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var (sessionManager, _, sequence) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        // Sanity check
        sequence.NumberForRunway(firstFlight).ShouldBe(1);
        sequence.NumberInSequence(firstFlight).ShouldBe(1);
        sequence.NumberForRunway(secondFlight).ShouldBe(1);
        sequence.NumberInSequence(secondFlight).ShouldBe(2);

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        sequence.NumberForRunway(firstFlight).ShouldBe(1);
        sequence.NumberInSequence(firstFlight).ShouldBe(2);
        sequence.NumberForRunway(secondFlight).ShouldBe(1);
        sequence.NumberInSequence(secondFlight).ShouldBe(1);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirLandingTimesAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(20));
        secondFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(10));
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirFeederFixTimesAreReCalculated()
    {
        // Arrange
        var firstTtg = TimeSpan.FromMinutes(10);
        var secondTtg = TimeSpan.FromMinutes(15);

        // Zero pressure windows so all delay is absorbed enroute: STA_FF = ETA_FF + EnrouteDelay
        var firstTrajectory = new TerminalTrajectory(firstTtg, firstTtg, firstTtg);
        var secondTrajectory = new TerminalTrajectory(secondTtg, secondTtg, secondTtg);

        var firstEtaFf = _clock.UtcNow();              // ETA_FF = now+0m → natural LandingEstimate = now+10m
        var secondEtaFf = _clock.UtcNow().AddMinutes(5); // ETA_FF = now+5m → natural LandingEstimate = now+20m

        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithFeederFixEstimate(firstEtaFf)
            .WithTrajectory(firstTrajectory)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithFeederFixEstimate(secondEtaFf)
            .WithTrajectory(secondTrajectory)
            .Build();

        // TTGs here are different to demonstrate that feeder fix times are re-calculated
        var trajectoryService = new MockTrajectoryService()
            .WithTrajectory().OnRunway("34L").Returns(firstTrajectory)
            .WithTrajectory().OnRunway("34R").Returns(secondTrajectory);

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        // Insert triggers Schedule(), which overwrites LandingTime with natural estimates.
        // Manually apply pre-delays so both flights have positive enroute delay after the swap.
        // firstFlight STA = now+20m; secondFlight STA = now+30m
        firstFlight.SetSequenceData(_clock.UtcNow().AddMinutes(20), firstFlight.FeederFixEstimate, ControlAction.NoDelay, FlowControls.HighSpeed, TimeSpan.Zero);
        secondFlight.SetSequenceData(_clock.UtcNow().AddMinutes(30), secondFlight.FeederFixEstimate, ControlAction.NoDelay, FlowControls.HighSpeed, TimeSpan.Zero);

        var handler = GetHandler(sessionManager);
        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert: STA_FF = ETA_FF + EnrouteDelay (all delay goes enroute — zero TMA pressure windows)
        // firstFlight: gets STA=now+30m, LandingEstimate=firstEtaFf+secondTtg=now+15m → 15m enroute delay
        firstFlight.FeederFixTime.ShouldBe(firstEtaFf.AddMinutes(15));   // now+15m
        // secondFlight: gets STA=now+20m, LandingEstimate=secondEtaFf+firstTtg=now+15m → 5m enroute delay
        secondFlight.FeederFixTime.ShouldBe(secondEtaFf.AddMinutes(5)); // now+10m
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheirRunwaysAreSwapped()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.AssignedRunwayIdentifier.ShouldBe("34R");
        secondFlight.AssignedRunwayIdentifier.ShouldBe("34L");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndTheyAreUnstable_TheyBecomeStable()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithState(State.Unstable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(secondFlight, firstFlight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        firstFlight.State.ShouldBe(State.Stable);
        secondFlight.State.ShouldBe(State.Stable);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndFirstFlightDoesNotExist_AnErrorIsThrown()
    {
        // Arrange
        var flight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA1 not found");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndSecondFlightDoesNotExist_AnErrorIsThrown()
    {
        // Arrange
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .WithState(State.Unstable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act & Assert
        var exception = await Should.ThrowAsync<MaestroException>(
            () => handler.Handle(request, CancellationToken.None));

        exception.Message.ShouldBe("QFA2 not found");
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_AndTheyAreNotUnstable_TheirStateRemainsUnchanged()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithFeederFixEstimate(_clock.UtcNow())
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(5))
            .WithLandingTime(_clock.UtcNow().AddMinutes(5))
            .WithState(State.Frozen)
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithFeederFixEstimate(_clock.UtcNow().AddMinutes(10))
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .WithLandingTime(_clock.UtcNow().AddMinutes(20))
            .WithState(State.SuperStable)
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.State.ShouldBe(State.Frozen);
        secondFlight.State.ShouldBe(State.SuperStable);
    }

    [Fact]
    public async Task WhenSwappingTwoFlights_TheSequenceIsNotRecomputed()
    {
        // Arrange
        var firstFlight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(10))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(20))
            .Build();

        var thirdFlight = new FlightBuilder("QFA2")
            .WithRunway("34R")
            .WithLandingEstimate(_clock.UtcNow().AddMinutes(30))
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight, thirdFlight))
            .Build();

        // Artificial 10-minute delay to ensure recomputation is not performed
        thirdFlight.SetSequenceData(_clock.UtcNow().AddMinutes(40), thirdFlight.FeederFixEstimate, ControlAction.NoDelay, FlowControls.ReduceSpeed, TimeSpan.Zero);

        var handler = GetHandler(sessionManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        thirdFlight.LandingTime.ShouldBe(_clock.UtcNow().AddMinutes(40));
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
        var flight2 = new FlightBuilder("QFA2")
            .WithFeederFix("BOREE")
            .WithLandingEstimate(now.AddMinutes(15))
            .WithLandingTime(now.AddMinutes(15))
            .WithRunway("34R")
            .Build();

        var (sessionManager, _, _) = new SessionBuilder(CreateAirportConfiguration())
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var airportConfiguration = CreateAirportConfiguration();
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);

        var handler = new SwapFlightsRequestHandler(
            sessionManager,
            slaveConnectionManager,
            configProvider,
            mediator,
            _clock,
            Substitute.For<ILogger>());

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        slaveConnectionManager.Connection.InvokedRequests.Count.ShouldBe(1, "Request should be relayed to master");
        slaveConnectionManager.Connection.InvokedRequests[0].ShouldBe(request, "The relayed request should match the original request");
        flight1.AssignedRunwayIdentifier.ShouldBe("34L", "The runways of the flights should not have changed");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R", "The runways of the flights should not have changed");
        flight1.LandingTime.ShouldBe(now.AddMinutes(10), "The landing times of the flights should not have changed");
        flight2.LandingTime.ShouldBe(now.AddMinutes(15), "The landing times of the flights should not have changed");
    }

    static AirportConfiguration CreateAirportConfiguration()
    {
        return new AirportConfigurationBuilder("YSSY")
            .WithRunways("34L", "34R")
            .WithFeederFixes("RIVET", "MARLN", "BOREE")
            .WithRunwayMode("34IVA",
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = DefaultLandingRateSeconds,
                    FeederFixes = ["RIVET"]
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = DefaultLandingRateSeconds,
                    FeederFixes = ["MARLN", "BOREE"]
                })
            .WithTrajectory("RIVET", "34L", 15)
            .WithTrajectory("MARLN", "34R", 15)
            .WithTrajectory("BOREE", "34R", 15)
            .Build();
    }

    SwapFlightsRequestHandler GetHandler(ISessionManager sessionManager)
    {
        var airportConfiguration = CreateAirportConfiguration();
        var configProvider = new AirportConfigurationProvider([airportConfiguration]);

        return new SwapFlightsRequestHandler(
            sessionManager,
            new MockLocalConnectionManager(),
            configProvider,
            Substitute.For<MediatR.IMediator>(),
            _clock,
            Substitute.For<ILogger>());
    }
}
