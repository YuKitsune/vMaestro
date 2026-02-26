using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class TrajectoryServiceTests(AirportConfigurationFixture airportConfigurationFixture, ClockFixture clockFixture)
    : IClassFixture<AirportConfigurationFixture>, IClassFixture<ClockFixture>
{
    [Fact]
    public void GetTrajectory_ReturnsTrajectoryFromArrivalLookup()
    {
        // Arrange
        var expectedTrajectory = new Trajectory(TimeSpan.FromMinutes(15));
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetTrajectory(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<AircraftCategory>()).Returns(expectedTrajectory);

        var trajectoryService = new TrajectoryService(
            arrivalLookup,
            Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "A");

        // Assert
        trajectory.ShouldBe(expectedTrajectory);
    }

    [Fact]
    public void GetTrajectory_WhenNoTrajectoryFound_ReturnsAverageTrajectory()
    {
        // Arrange
        var averageTrajectory = new Trajectory(TimeSpan.FromMinutes(20));
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetTrajectory(
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<string[]>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<AircraftCategory>()).Returns((Trajectory?)null);
        arrivalLookup.GetAverageTrajectory("YSSY").Returns(averageTrajectory);

        var trajectoryService = new TrajectoryService(
            arrivalLookup,
            Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "A");

        // Assert
        trajectory.ShouldBe(averageTrajectory);
    }

    [Fact]
    public void GetAverageTrajectory_ReturnsAverageFromArrivalLookup()
    {
        // Arrange
        var expectedTrajectory = new Trajectory(TimeSpan.FromMinutes(18));
        var arrivalLookup = Substitute.For<IArrivalLookup>();
        arrivalLookup.GetAverageTrajectory("YSSY").Returns(expectedTrajectory);

        var trajectoryService = new TrajectoryService(
            arrivalLookup,
            Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert
        trajectory.ShouldBe(expectedTrajectory);
    }
}
