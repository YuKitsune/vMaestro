using Maestro.Contracts.Shared;
using Maestro.Core.Configuration;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
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
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new(0, 0));

        // Assert
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void GetTrajectory_WhenNoMatch_ReturnsAverageTTG()
    {
        // Arrange: two trajectories at 15 and 20 minutes → average = 17.5 minutes
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunways("34L", "34R")
            .WithTrajectory("RIVET", "34L", 15)
            .WithTrajectory("BOREE", "34R", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("WELSH") // Different feeder fix, no match
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act — no match, should fall back to average of all configured trajectories
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new(0 ,0));

        // Assert: (15 + 20) / 2 = 17.5 minutes
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(17.5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_WithApproachType_ReturnsMatchingTrajectory()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", "A", "34L", 18)
            .WithTrajectory("RIVET", "B", "34L", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "A", [], new(0 ,0));

        // Assert
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(18));
    }

    [Fact]
    public void GetAverageTrajectory_ComputesAverageAcrossAllTrajectories()
    {
        // Arrange: 4 trajectories at 10, 12, 20, 22 minutes → average = 16 minutes
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET", "BOREE")
            .WithRunways("34L", "34R")
            .WithTrajectory("RIVET", "34L", 10)
            .WithTrajectory("RIVET", "34R", 12)
            .WithTrajectory("BOREE", "34L", 20)
            .WithTrajectory("BOREE", "34R", 22)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert: (10 + 12 + 20 + 22) / 4 = 16 minutes
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(16), TimeSpan.FromSeconds(1));
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
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert — default is 20 minutes
        trajectory.TimeToGo.ShouldBe(TimeSpan.FromMinutes(20));
    }

    [Fact]
    public void GetApproachTypes_ReturnsMatchingApproachTypes()
    {
        // Arrange
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory("RIVET", "A", "34L", 18)
            .WithTrajectory("RIVET", "B", "34L", 20)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

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
            .WithTrajectory("RIVET", "A", "34L", 18)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, Substitute.For<IPerformanceLookup>(), Substitute.For<ILogger>());

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

    [Fact]
    public void GetTrajectory_WindComputation_ZeroWindGivesExpectedTTG()
    {
        // Arrange: with zero wind, TTG = distance / TAS
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(0, 0));

        // Assert
        trajectory.TimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_WindComputation_HeadwindIncreasesTTG()
    {
        // Arrange: segment with track 0 (due north), wind from 0° at 30 kts = pure headwind
        const int approachSpeedKnots = 150;
        const double windSpeed = 30;
        const double distanceNm = 25.0;
        var expectedGroundSpeed = approachSpeedKnots - windSpeed; // 120 kts
        var expectedTtg = TimeSpan.FromHours(distanceNm / expectedGroundSpeed);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Wind from north (0°) at 30 kts on a northbound track (0°) = pure headwind
        var headWind = new Wind(0, (int)windSpeed);

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], headWind);

        // Assert
        trajectory.TimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_WindComputation_TailwindDecreasesTTG()
    {
        // Arrange: segment with track 0 (due north), wind from 180° at 30 kts = pure tailwind
        const int approachSpeedKnots = 150;
        const double windSpeed = 30;
        const double distanceNm = 25.0;
        var expectedGroundSpeed = approachSpeedKnots + windSpeed; // 180 kts
        var expectedTtg = TimeSpan.FromHours(distanceNm / expectedGroundSpeed);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Wind from south (180°) at 30 kts on a northbound track (0°) = pure tailwind
        var tailWind = new Wind(180, (int)windSpeed);

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], tailWind);

        // Assert
        trajectory.TimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_WindComputation_CrosswindHasNoEffect()
    {
        // Arrange: segment with track 0 (due north), wind from 90° (east) = pure crosswind
        // cos(0° - 90°) = cos(-90°) = 0, so headwind component = 0
        const int approachSpeedKnots = 150;
        const double windSpeed = 30;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots); // same as zero wind

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        var crossWind = new Wind(90, (int)windSpeed);

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], crossWind);

        // Assert
        trajectory.TimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_NoPressureBranch_PressureEqualsTTG()
    {
        // Arrange
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
                // No Pressure or MaxPressure branch configured
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(0, 0));

        // Assert: no pressure branch configured, so P and Pmax both fall back to TTG
        trajectory.TimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.Pressure.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.MaxPressure.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_Pressure_IsCumulativeTimeFromFeederFixToThresholdViaPressurePath()
    {
        // Arrange: two segments — first is normal, second has Pressure: true
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0; // each segment
        var segmentTime = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments =
                [
                    new TrajectorySegmentConfiguration { Identifier = "LEG1", Track = 0, DistanceNM = distanceNm },
                ],
                Pressure = new TrajectoryBranch
                {
                    After = "LEG1",
                    Segments =
                    [
                        new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm}
                    ]
                }
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(0, 0));

        // Assert: TTG  = cumulative FF->LEG1->threshold (base path)
        //         P    = cumulative FF->LEG1->pressure segment->threshold
        //         Pmax = P (no MaxPressure configured, falls back to P)
        trajectory.TimeToGo.ShouldBe(segmentTime, TimeSpan.FromSeconds(1));
        trajectory.Pressure.ShouldBe(segmentTime + segmentTime, TimeSpan.FromSeconds(1));
        trajectory.MaxPressure.ShouldBe(segmentTime + segmentTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_MaxPressure_IsCumulativeTimeFromFeederFixToThresholdViaMaxPressurePath()
    {
        // Arrange: three segments — normal, pressure, max-pressure
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0; // each segment
        var segmentTime = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments =
                [
                    new TrajectorySegmentConfiguration { Identifier = "LEG1", Track = 0, DistanceNM = distanceNm },
                ],
                Pressure = new TrajectoryBranch
                {
                    After = "LEG1",
                    Segments =
                    [
                        new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm}
                    ]
                },
                MaxPressure = new TrajectoryBranch
                {
                    After = "LEG1",
                    Segments =
                    [
                        new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm},
                        new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm}
                    ]
                }
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetApproachSpeed(Arg.Any<string>()).Returns(approachSpeedKnots);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(0, 0));

        // Assert: TTG  = cumulative FF->LEG1->threshold (base path)
        //         P    = cumulative FF->LEG1->pressure segment->threshold
        //         Pmax = cumulative FF->LEG1->maxpressure seg 1->maxpressure seg 2->threshold
        trajectory.TimeToGo.ShouldBe(segmentTime, TimeSpan.FromSeconds(1));
        trajectory.Pressure.ShouldBe(segmentTime * 2, TimeSpan.FromSeconds(1));
        trajectory.MaxPressure.ShouldBe(segmentTime * 3, TimeSpan.FromSeconds(1));
    }
}
