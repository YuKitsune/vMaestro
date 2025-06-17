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

        sequence.Add(flight);

        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .Returns(new TestExclusiveSequence(sequence));
        
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            sequenceProvider,
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");
        
        // Act
        await handler.Handle(request, CancellationToken.None);
        
        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);
        
        await mediator.Received().Send(Arg.Any<RecomputeRequest>(), Arg.Any<CancellationToken>());
    }
    
}