using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Maestro.Core.Tests.Handlers;

public class ChangeRunwayRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    [Fact]
    public async Task WhenChangingRunway_TheRunwayIsChanged()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .Build();

        await sequence.Add(flight, CancellationToken.None);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.TryGetSequence(Arg.Is("YSSY")).Returns(sequence);
        
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            sequenceProvider,
            mediator,
            new NullLogger<RecomputeRequestHandler>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");
        
        // Act
        await handler.Handle(request, CancellationToken.None);
        
        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);
        
        await mediator.Received().Send(Arg.Any<RecomputeRequest>(), Arg.Any<CancellationToken>());
    }
    
}