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

public class MakePendingRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    readonly AirportConfiguration _airportConfiguration = airportConfigurationFixture.Instance;

    [Fact]
    public async Task WhenMakingFlightPending_AllRequiredUpdatesArePerformed()
    {
        // Arrange
        var now = clockFixture.Instance.UtcNow();
        var flight = new FlightBuilder("QFA123")
            .WithState(State.Stable)
            .WithLandingTime(now.AddMinutes(10))
            .WithLandingEstimate(now.AddMinutes(8))
            .WithFeederFixTime(now.AddMinutes(5))
            .WithFeederFixEstimate(now.AddMinutes(3))
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        var originalEstimate = flight.EstimatedLandingTime;
        var originalFeederFixEstimate = flight.EstimatedFeederFixTime;
        var originalScheduledTime = flight.ScheduledLandingTime;

        var scheduler = Substitute.For<IScheduler>();
        var handler = GetRequestHandler(sequence, scheduler);
        var request = new MakePendingRequest("YSSY", "QFA123");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.State.ShouldBe(State.Pending);
        flight.EstimatedLandingTime.ShouldNotBe(originalEstimate);
        flight.EstimatedFeederFixTime.ShouldNotBe(originalFeederFixEstimate);
        flight.ScheduledLandingTime.ShouldNotBe(originalScheduledTime);
        scheduler.Received(1).Schedule(sequence);
    }

    [Fact]
    public async Task WhenFlightNotFound_WarningIsLoggedAndHandlerReturns()
    {
        // Arrange
        var sequence = new SequenceBuilder(_airportConfiguration)
            .Build();

        var logger = Substitute.For<ILogger>();
        var handler = GetRequestHandler(sequence, logger: logger);
        var request = new MakePendingRequest("YSSY", "NONEXISTENT");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        logger.Received(1).Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", "NONEXISTENT", "YSSY");
    }

    MakePendingRequestHandler GetRequestHandler(Sequence sequence, IScheduler? scheduler = null, ILogger? logger = null)
    {
        var sequenceProvider = new MockSequenceProvider(sequence);
        scheduler ??= Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();
        logger ??= Substitute.For<ILogger>();
        return new MakePendingRequestHandler(sequenceProvider, clockFixture.Instance, scheduler, mediator, logger);
    }
}
