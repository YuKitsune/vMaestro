using Maestro.Core.Handlers;
using Maestro.Core.Infrastructure;
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
        var flight = new FlightBuilder("QFA1")
            .WithRunway("34L")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var sequenceProvider = new MockSequenceProvider(sequence);

        var scheduler = Substitute.For<IScheduler>();
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            sequenceProvider,
            scheduler,
            Substitute.For<IClock>(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", "34R");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.AssignedRunwayIdentifier.ShouldBe("34R");
        flight.RunwayManuallyAssigned.ShouldBe(true);

        scheduler.Received(1).Recompute(flight, sequence);
    }
}
