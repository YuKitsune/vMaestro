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

// TODO:
// - When rerouted to a new feeder fix, the feeder fix and estimates are updated
// - When recomputing a stable flight, it's position in sequence changes
// - Runway is re-assigned (and overrides manual runway)
// - When a runway is manually assigned, it is re-assigned (?)
// - When a manual landing time was assigned, it is removed

public class RecomputeRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture)
{
    [Fact]
    public async Task WhenRecomputeIsRequested_FlightIsUpdated()
    {
        // Arrange
        var scheduler = Substitute.For<IScheduler>();

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .WithFlight(flight)
            .Build();

        var handler = new RecomputeRequestHandler(
            new MockSequenceProvider(sequence),
            scheduler,
            Substitute.For<IMediator>(),
            Substitute.For<ILogger>());

        var request = new RecomputeRequest("YSSY", "QFA1");

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight.NeedsRecompute.ShouldBe(true);
        scheduler.Received(1).Schedule(Arg.Is(sequence));
    }
}
