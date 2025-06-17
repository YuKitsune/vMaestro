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

public class RecomputeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    [Fact]
    public async Task WhenRecomputeIsRequested_FlightIsUpdated()
    {
        // Arrange
        var sequence = new Sequence(airportConfigurationFixture.Instance);
        
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .Build();
        
        sequence.Add(flight);
        
        var handler = GetRequestHandler(sequence);
        var request = new RecomputeRequest("YSSY", "QFA1");
        
        // Act
        await handler.Handle(request, CancellationToken.None);
        
        // Assert
        flight.NeedsRecompute.ShouldBe(true);
    }

    RecomputeRequestHandler GetRequestHandler(Sequence sequence)
    {
        var sequenceProvider = Substitute.For<ISequenceProvider>();
        sequenceProvider.GetSequence(Arg.Is("YSSY"), Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(new TestExclusiveSequence(sequence));
        
        var mediator = Substitute.For<IMediator>();
        
        return new RecomputeRequestHandler(
            sequenceProvider,
            mediator,
            Substitute.For<ILogger>());
    }
}