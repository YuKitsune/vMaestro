using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class RecomputeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    [Fact]
    public async Task WhenRoutedToAnotherFeederFix_FlightIsUpdated()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .Build();
        
        await sequence.Add(flight, CancellationToken.None);
        
        // Change the feeder fix (emulate a re-route)
        var newEtaFf = DateTimeOffset.Now.AddMinutes(5);
        flight.UpdatePosition(
            new FlightPosition(new Coordinate(0, 0), 25_000, VerticalTrack.Descending, 280),
            [new FixEstimate("WELSH", newEtaFf)]);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");
        
        // Act
        await handler.Handle(request, CancellationToken.None);
        
        // Assert
        flight.FeederFixIdentifier.ShouldBe("WELSH");
        flight.EstimatedFeederFixTime.ShouldBe(newEtaFf);
    }
    
    // TODO: Verify this behavior is desired.
    [Fact]
    public async Task WhenARunwayHasBeenManuallyAssigned_ItIsNotOverridden()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .Build();
        
        await sequence.Add(flight, CancellationToken.None);
        
        // Change the runway
        flight.SetRunway("34R", manual: true);

        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");
        
        // Act
        await handler.Handle(request, CancellationToken.None);
        
        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);
    }
    
    [Fact]
    public async Task WhenACustomFeederFixEstimateWasProvided_ItIsOverridden()
    {
        await Task.CompletedTask;
        Assert.Fail("Stub");
    }

    RecomputeRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).ReturnsForAnyArgs(sequence);
        
        var runwayAssigner = Substitute.For<IRunwayAssigner>();
        var estimateProvider = Substitute.For<IEstimateProvider>();
        var scheduler = Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();
        var logger = new Logger<RecomputeRequestHandler>(NullLoggerFactory.Instance);
        
        return new RecomputeRequestHandler(
            sequenceProvider,
            runwayAssigner,
            estimateProvider,
            scheduler,
            mediator,
            logger);
    }
}