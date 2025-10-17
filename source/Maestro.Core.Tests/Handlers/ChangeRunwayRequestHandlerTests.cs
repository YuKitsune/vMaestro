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

public class ChangeRunwayRequestHandlerTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
{
    [Fact]
    public async Task WhenChangingRunway_TheRunwayIsChanged()
    {
        var now = clockFixture.Instance.UtcNow();

        // Arrange
        var flight1 = new FlightBuilder("QFA1")
            .WithLandingEstimate(now.AddMinutes(11))
            .WithLandingTime(now.AddMinutes(11))
            .WithRunway("34L")
            .Build();

        var flight2 = new FlightBuilder("QFA2")
            .WithLandingEstimate(now.AddMinutes(10))
            .WithLandingTime(now.AddMinutes(10))
            .WithRunway("34R")
            .Build();

        var sequence = new SequenceBuilder(airportConfigurationFixture.Instance)
            .Build(); // Uses default runway mode with both 34L and 34R

        sequence.Insert(flight1, flight1.LandingEstimate);
        sequence.Insert(flight2, flight2.LandingEstimate);

        // Verify initial runway assignments and ordering
        flight1.AssignedRunwayIdentifier.ShouldBe("34L");
        flight2.AssignedRunwayIdentifier.ShouldBe("34R");
        sequence.NumberForRunway(flight1).ShouldBe(1, "QFA1 should be #1 on 34L initially");
        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R initially");

        var sessionManager = new MockLocalSessionManager(sequence);
        var mediator = Substitute.For<IMediator>();

        var handler = new ChangeRunwayRequestHandler(
            sessionManager,
            Substitute.For<IArrivalLookup>(),
            Substitute.For<IClock>(),
            mediator,
            Substitute.For<ILogger>());

        var request = new ChangeRunwayRequest("YSSY", "QFA1", new RunwayDto("34L", "V", 180, []));

        // Act
        await handler.Handle(request, CancellationToken.None);

        // Assert
        flight1.AssignedRunwayIdentifier.ShouldBe("34R", "QFA1 should be assigned to 34R");
        flight1.RunwayManuallyAssigned.ShouldBe(true, "runway should be marked as manually assigned");

        flight1.ApproachType.ShouldBe("V", "QFA1 should have the correct approach type for 34R");

        flight1.LandingTime.ShouldBe(flight2.LandingTime.Add(airportConfigurationFixture.AcceptanceRate), "QFA1 should be delayed to maintain separation behind QFA2");
        flight1.TotalDelay.ShouldBe(TimeSpan.FromMinutes(2));

        // Verify QFA1 is now scheduled on 34R and positioned appropriately
        sequence.NumberForRunway(flight2).ShouldBe(1, "QFA2 should be #1 on 34R");
        sequence.NumberForRunway(flight1).ShouldBe(2, "QFA1 should be #2 on 34R after moving to 34R");
    }
}
