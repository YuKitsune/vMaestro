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
    // Single-band flat profile — speed is constant regardless of DTG.
    // Used to keep existing geometry-based tests deterministic.
    static SpeedBand[] FlatProfile(int speedKnots) =>
        [new SpeedBand { ThresholdNM = 0, SpeedKnots = speedKnots }];

    static IPerformanceLookup MockLookupWithFlatProfile(int speedKnots = 150)
    {
        var lookup = Substitute.For<IPerformanceLookup>();
        lookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(speedKnots));
        return lookup;
    }

    // Trajectory geometry copied verbatim from Maestro.yaml: YSSY TerminalTrajectories, FeederFix=RIVET, RunwayIdentifier=34L.
    static readonly TerminalTrajectoryConfiguration RivetArrival34L = new()
    {
        FeederFix = "RIVET",
        RunwayIdentifier = "34L",
        Segments =
        [
            new() { Identifier = "BIGEM", Track = 61.6,  DistanceNM = 12.6 },
            new() { Identifier = "TAMMI", Track = 61.5,  DistanceNM = 9.9  },
            new() { Identifier = "BOOGI", Track = 61.5,  DistanceNM = 10.0 },
            new() { Identifier = "DUDOK", Track = 133.6, DistanceNM = 5.0  },
            new() { Identifier = "NASHO", Track = 167.9, DistanceNM = 7.1  },
            new() { Identifier = "34LDW", Track = 117.0, DistanceNM = 5.0  },
            new() { Identifier = "34LBS", Track = 15.0,  DistanceNM = 5.0  },
            new() { Identifier = "34LF",  Track = 335.0, DistanceNM = 13.0 },
        ]
    };

    // Speed profile copied verbatim from Maestro.yaml: AircraftPerformance, AircraftTypes=[Jet, DH8D].
    static readonly SpeedBand[] JetSpeedProfile =
    [
        new() { ThresholdNM = 45, SpeedKnots = 330 },
        new() { ThresholdNM = 35, SpeedKnots = 320 },
        new() { ThresholdNM = 25, SpeedKnots = 285 },
        new() { ThresholdNM = 15, SpeedKnots = 250 },
        new() { ThresholdNM = 6,  SpeedKnots = 205 },
        new() { ThresholdNM = 2,  SpeedKnots = 160 },
        new() { ThresholdNM = 0,  SpeedKnots = 140 },
    ];

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
        var trajectoryService = new TrajectoryService(provider, MockLookupWithFlatProfile(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new(0, 0));

        // Assert
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(15));
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
        var trajectoryService = new TrajectoryService(provider, MockLookupWithFlatProfile(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("WELSH") // Different feeder fix, no match
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act — no match, should fall back to average of all configured trajectories
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new(0, 0));

        // Assert: (15 + 20) / 2 = 17.5 minutes
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(17.5), TimeSpan.FromSeconds(1));
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
        var trajectoryService = new TrajectoryService(provider, MockLookupWithFlatProfile(), Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(10))
            .WithRunway("34L")
            .WithApproachType("A")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "A", [], new(0, 0));

        // Assert
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(18));
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
        var trajectoryService = new TrajectoryService(provider, MockLookupWithFlatProfile(), Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert: (10 + 12 + 20 + 22) / 4 = 16 minutes
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(16), TimeSpan.FromSeconds(1));
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
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(20));
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
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
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
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
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
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
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
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
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
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
                // No Pressure or MaxPressure branch configured
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_Pressure_IsCumulativeTimeFromFeederFixToThresholdViaPressurePath()
    {
        // Arrange: base segment "RIVET" + pressure branch branching after "RIVET"
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var segmentTime = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TerminalTrajectoryConfiguration
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
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(segmentTime, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(segmentTime + segmentTime, TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(segmentTime + segmentTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_MaxPressure_IsCumulativeTimeFromFeederFixToThresholdViaMaxPressurePath()
    {
        // Arrange: three segments — normal, pressure, max-pressure
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var segmentTime = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TerminalTrajectoryConfiguration
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
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(segmentTime, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(segmentTime * 2, TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(segmentTime * 3, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_HardCodedPressure_IsAddedToTTG()
    {
        // Arrange: no pressure trajectories, so the hard-coded delays on the trajectory apply
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }],
                PressureSeconds = 120,
                MaxPressureSeconds = 300
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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

        // Assert: P and Pmax are the TTG plus the hard-coded delays
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_NoPressureOnTrajectory_UsesAirportDefaults()
    {
        // Arrange: the trajectory has no pressure of any kind, so the airport defaults apply
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithDefaultPressureSeconds(60)
            .WithDefaultMaxPressureSeconds(240)
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_HardCodedPressure_OverridesAirportDefaults()
    {
        // Arrange
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithDefaultPressureSeconds(60)
            .WithDefaultMaxPressureSeconds(240)
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }],
                PressureSeconds = 120,
                MaxPressureSeconds = 300
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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

        // Assert: the trajectory values win over the airport defaults
        trajectory.PressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_PressureTrajectory_OverridesHardCodedPressure()
    {
        // Arrange: a pressure trajectory and a hard-coded maximum pressure delay.
        // P comes from the segments, Pmax comes from the hard-coded value.
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var segmentTime = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithDefaultPressureSeconds(60)
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments =
                [
                    new TrajectorySegmentConfiguration { Identifier = "LEG1", Track = 0, DistanceNM = distanceNm }
                ],
                Pressure = new TrajectoryBranch
                {
                    After = "LEG1",
                    Segments =
                    [
                        new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }
                    ]
                },
                PressureSeconds = 120,
                MaxPressureSeconds = 900
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.NormalTimeToGo.ShouldBe(segmentTime, TimeSpan.FromSeconds(1));
        trajectory.PressureTimeToGo.ShouldBe(segmentTime * 2, TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(segmentTime + TimeSpan.FromSeconds(900), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetTrajectory_NoMaxPressure_FallsBackToPressure()
    {
        // Arrange: only a pressure delay is given, so Pmax matches P
        const int approachSpeedKnots = 150;
        const double distanceNm = 25.0;
        var expectedTtg = TimeSpan.FromHours(distanceNm / approachSpeedKnots);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithDefaultPressureSeconds(180)
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(FlatProfile(approachSpeedKnots));

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
        trajectory.PressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(1));
        trajectory.MaxPressureTimeToGo.ShouldBe(expectedTtg + TimeSpan.FromSeconds(180), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetAverageTrajectory_WithNoTrajectories_UsesDefaultPressure()
    {
        // Arrange: an airport with no trajectories at all, relying entirely on the defaults
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithDefaultPressureSeconds(120)
            .WithDefaultMaxPressureSeconds(600)
            .Build();

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, MockLookupWithFlatProfile(), Substitute.For<ILogger>());

        // Act
        var trajectory = trajectoryService.GetAverageTrajectory("YSSY");

        // Assert
        var defaultTtg = TimeSpan.FromMinutes(airportConfiguration.DefaultTimeToGoMinutes);
        trajectory.NormalTimeToGo.ShouldBe(defaultTtg);
        trajectory.PressureTimeToGo.ShouldBe(defaultTtg + TimeSpan.FromSeconds(120));
        trajectory.MaxPressureTimeToGo.ShouldBe(defaultTtg + TimeSpan.FromSeconds(600));
    }

    [Fact]
    public void GetTrajectory_SpeedBand_SplitsSegmentAtBandBoundary()
    {
        // Arrange: one 30 NM segment from DTG=30 to DTG=0.
        // Speed profile: 200 kts when DTG > 15 NM, 100 kts when DTG <= 15 NM.
        // Expected: first 15 NM at 200 kts + last 15 NM at 100 kts.
        const double distanceNm = 30.0;
        var speedBands = new SpeedBand[]
        {
            new() { ThresholdNM = 15, SpeedKnots = 200 },
            new() { ThresholdNM = 0,  SpeedKnots = 100 },
        };

        var expectedTtg = TimeSpan.FromHours(15.0 / 200 + 15.0 / 100);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments = [new TrajectorySegmentConfiguration { Track = 0, DistanceNM = distanceNm }]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(speedBands);

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void GetTrajectory_SpeedBand_MultipleSegmentsUseDtgRelativeToFullRoute()
    {
        // Arrange: two 20 NM segments (total 40 NM).
        // Speed profile: 200 kts when DTG > 20 NM, 100 kts when DTG <= 20 NM.
        // Segment 1 (DTG 40→20): entirely in the 200 kt band → 20/200 hours.
        // Segment 2 (DTG 20→0): entirely in the 100 kt band → 20/100 hours.
        const double segmentNm = 20.0;
        var speedBands = new SpeedBand[]
        {
            new() { ThresholdNM = 20, SpeedKnots = 200 },
            new() { ThresholdNM = 0,  SpeedKnots = 100 },
        };

        // Seg 1 starts at DTG=40 → 40 > 20, so 200 kts for the first segment.
        // Seg 2 starts at DTG=20 → 20 > 20 is false, so 100 kts for the second segment.
        var expectedTtg = TimeSpan.FromHours(segmentNm / 200.0 + segmentNm / 100.0);

        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(new TerminalTrajectoryConfiguration
            {
                FeederFix = "RIVET",
                RunwayIdentifier = "34L",
                Segments =
                [
                    new TrajectorySegmentConfiguration { Track = 0, DistanceNM = segmentNm },
                    new TrajectorySegmentConfiguration { Track = 0, DistanceNM = segmentNm },
                ]
            })
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(speedBands);

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
        trajectory.NormalTimeToGo.ShouldBe(expectedTtg, TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void CalculateEtiNilWind()
    {
        // Arrange: real YSSY RIVET→34L geometry with the Jet speed profile, zero wind.
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(RivetArrival34L)
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(JetSpeedProfile);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(0, 0));

        // Assert
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void CalculateEti_EasterlyWind()
    {
        // Arrange: real YSSY RIVET→34L geometry with the Jet speed profile.
        // 50 kt wind from 090 (east) creates a headwind for the majority of the arrival, resulting in a longer TTG.
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(RivetArrival34L)
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(JetSpeedProfile);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(90, 50));

        // Assert
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(16), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void CalculateEti_WesterlyWind()
    {
        // Arrange: real YSSY RIVET→34L geometry with the Jet speed profile.
        // 50 kt wind from 270 (west) creates a tailwind for the majority of the arrival, resulting in a shorter TTG.
        var airportConfiguration = new AirportConfigurationBuilder("YSSY")
            .WithFeederFixes("RIVET")
            .WithRunways("34L")
            .WithTrajectory(RivetArrival34L)
            .Build();

        var performanceLookup = Substitute.For<IPerformanceLookup>();
        performanceLookup.GetSpeedProfile(Arg.Any<AircraftPerformanceData>()).Returns(JetSpeedProfile);

        var provider = new AirportConfigurationProvider([airportConfiguration]);
        var trajectoryService = new TrajectoryService(provider, performanceLookup, Substitute.For<ILogger>());

        var flight = new FlightBuilder("QFA1")
            .WithFeederFix("RIVET")
            .WithFeederFixEstimate(clockFixture.Instance.UtcNow().AddMinutes(20))
            .WithRunway("34L")
            .WithApproachType("")
            .Build();

        // Act
        var trajectory = trajectoryService.GetTrajectory(flight, "34L", "", [], new Wind(270, 50));

        // Assert
        trajectory.NormalTimeToGo.ShouldBe(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));
    }
}
