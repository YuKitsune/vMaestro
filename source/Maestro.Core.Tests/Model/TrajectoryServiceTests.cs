using Maestro.Core.Configuration;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;

namespace Maestro.Core.Tests.Model;

public class TrajectoryServiceTests(ClockFixture clockFixture)
    : IClassFixture<ClockFixture>
{
    [Fact]
    public void GetTrajectory_ReturnsMatchingTrajectory()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", "34L", 15)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "");

        // Assert
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void GetTrajectory_WhenNoMatch_ReturnsAverageTrajectory()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunways("34L", "34R")
            .WithTrajectory("RIVET", "34L", 15)
            .WithTrajectory("BOREE", "34R", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("WELSH") // Different feeder fix, no match
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "");

        // Assert - should return average of all trajectories (15 + 20) / 2 = 17.5
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(17.5));
    }

    [Fact]
    public void GetTrajectory_WithApproachType_ReturnsMatchingTrajectory()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", [new AllAircraftTypesDescriptor()], "A", "34L", 18)
            .WithTrajectory("RIVET", [new AllAircraftTypesDescriptor()], "B", "34L", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "A");

        // Assert
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(18));
    }

    [Fact]
    public void GetTrajectory_WithAircraftCategory_ReturnsMatchingTrajectory()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", [new AircraftCategoryDescriptor(AircraftCategory.Jet)], "34L", 15)
            .WithTrajectory("RIVET", [new AircraftCategoryDescriptor(AircraftCategory.NonJet)], "34L", 18)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        // Act
        var jetTrajectory = trajectoryService.GetTrajectory(
            new AircraftPerformanceData("B738", AircraftCategory.Jet, WakeCategory.Medium),
            "YSSY",
            "RIVET",
            "34L",
            "");

        var propTrajectory = trajectoryService.GetTrajectory(
            new AircraftPerformanceData("SF34", AircraftCategory.NonJet, WakeCategory.Light),
            "YSSY",
            "RIVET",
            "34L",
            "");

        // Assert
        jetTrajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(15));
        propTrajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(18));
    }

    [Fact]
    public void GetAverageTrajectory_ReturnsAverageOfAllTrajectories()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunways("34L", "34R")
            .WithTrajectory("RIVET", "34L", 10)
            .WithTrajectory("RIVET", "34R", 12)
            .WithTrajectory("BOREE", "34L", 20)
            .WithTrajectory("BOREE", "34R", 22)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert - average of 10, 12, 20, 22 = 16
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(16));
    }

    [Fact]
    public void GetAverageTrajectory_WhenNoTrajectories_ReturnsDefault()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert - default is 20 minutes
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void GetApproachTypes_ReturnsMatchingApproachTypes()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", [new AllAircraftTypesDescriptor()], "A", "34L", 18)
            .WithTrajectory("RIVET", [new AllAircraftTypesDescriptor()], "B", "34L", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        // Act
        var approachTypes = trajectoryService.GetApproachTypes(
            "YSSY",
            "RIVET",
            [],
            "34L",
            new AircraftPerformanceData("B738", AircraftCategory.Jet, WakeCategory.Medium));

        // Assert
        approachTypes.ShouldContain("A");
        approachTypes.ShouldContain("B");
    }

    [Fact]
    public void GetApproachTypes_WhenNoMatch_ReturnsEmpty()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", [new AllAircraftTypesDescriptor()], "A", "34L", 18)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<ILogger>());

        // Act
        var approachTypes = trajectoryService.GetApproachTypes(
            "YSSY",
            "BOREE", // Different feeder fix
            [],
            "34L",
            new AircraftPerformanceData("B738", AircraftCategory.Jet, WakeCategory.Medium));

        // Assert
        approachTypes.ShouldBeEmpty();
    }
}
