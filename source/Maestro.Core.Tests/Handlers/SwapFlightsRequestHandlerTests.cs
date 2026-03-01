using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Hosting;
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

public class SwapFlightsRequestHandlerTests(
    AirportConfigurationFixture airportConfigurationFixture,
    ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;
    readonly FixedClock _clock = clockFixture.Instance;

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

        var (instanceManager, _, _, sequence) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        // Sanity check
        sequence.NumberForRunway(firstFlight).ShouldBe(1);
        sequence.NumberInSequence(firstFlight).ShouldBe(1);
        sequence.NumberForRunway(secondFlight).ShouldBe(1);
        sequence.NumberInSequence(secondFlight).ShouldBe(2);

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(instanceManager);

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
        var firstLandingTime = _clock.UtcNow().AddMinutes(10);
        var firstTtg = TimeSpan.FromMinutes(10);
        var firstFeederFixTime = firstLandingTime.Subtract(firstTtg); // now + 0

        var secondLandingTime = _clock.UtcNow().AddMinutes(20);
        var secondTtg = TimeSpan.FromMinutes(15);
        var secondFeederFixTime = secondLandingTime.Subtract(secondTtg); // now + 5

        var firstFlight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34L")
            .WithFeederFixEstimate(firstFeederFixTime)
            .WithLandingTime(firstLandingTime)
            .WithTrajectory(new Trajectory(firstTtg))
            .Build();

        var secondFlight = new FlightBuilder("QFA2")
            .WithFeederFix("MARLN") // TODO: Remove when Sequence no longer re-assigns the runway
            .WithRunway("34R")
            .WithFeederFixEstimate(secondFeederFixTime)
            .WithLandingTime(secondLandingTime)
            .WithTrajectory(new Trajectory(secondTtg))
            .Build();

        // TTGs here are different to demonstrate that feeder fix times are re-calculated
        var trajectoryService = new MockTrajectoryService()
            .WithTrajectory().OnRunway("34L").Returns(new Trajectory(firstTtg))
            .WithTrajectory().OnRunway("34R").Returns(new Trajectory(secondTtg));

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithTrajectoryService(trajectoryService).WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(instanceManager);

        var request = new SwapFlightsRequest("YSSY", "QFA1", "QFA2");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        firstFlight.FeederFixTime.ShouldBe(secondLandingTime.Subtract(secondTtg));
        secondFlight.FeederFixTime.ShouldBe(firstLandingTime.Subtract(firstTtg));
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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(secondFlight, firstFlight))
            .Build();

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlight(flight))
            .Build();

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight))
            .Build();

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(_airportConfiguration)
            .WithSequence(s => s.WithFlightsInOrder(firstFlight, secondFlight, thirdFlight))
            .Build();

        // Artificial 10-minute delay to ensure recomputation is not performed
        thirdFlight.SetSequenceData(_clock.UtcNow().AddMinutes(40), FlowControls.ReduceSpeed);

        var handler = GetHandler(instanceManager);

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

        var (instanceManager, _, _, _) = new InstanceBuilder(airportConfigurationFixture.Instance)
            .WithSequence(s => s.WithClock(clockFixture.Instance).WithFlightsInOrder(flight1, flight2))
            .Build();

        var slaveConnectionManager = new MockSlaveConnectionManager();
        var mediator = Substitute.For<IMediator>();

        var handler = new SwapFlightsRequestHandler(
            instanceManager,
            slaveConnectionManager,
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

    SwapFlightsRequestHandler GetHandler(IMaestroInstanceManager instanceManager)
    {
        return new SwapFlightsRequestHandler(
            instanceManager,
            new MockLocalConnectionManager(),
            Substitute.For<MediatR.IMediator>(),
            _clock,
            Substitute.For<ILogger>());
    }
}
