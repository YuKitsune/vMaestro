using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Builders;
using Maestro.Core.Tests.Fixtures;
using NSubstitute;
using Serilog;
using Shouldly;
using RunwayRequirements = Maestro.Core.Configuration.RunwayRequirements;

namespace Maestro.Core.Tests;

// TODO: Merge this into scheduler tests (or separate the logic out of the Scheduler)

public class RunwayRequirementsTests : IClassFixture<ClockFixture>
{
    readonly AirportConfiguration _airportConfiguration;
    readonly Scheduler _scheduler;

    public RunwayRequirementsTests(ClockFixture clockFixture)
    {
        var runwayScoreCalculator = new RunwayScoreCalculator();
        _airportConfiguration = new AirportConfiguration
        {
            Identifier = "YBBN",
            MinimumRadarEstimateRange = 0,
            FeederFixes = [],
            Runways =
            [
                new()
                {
                    Identifier = "19L",
                    LandingRateSeconds = 180,
                    Requirements = new RunwayRequirements
                    {
                        FeederFixes = ["GOMOL"]
                    }
                },
                new()
                {
                    Identifier = "19R",
                    LandingRateSeconds = 180,
                    Requirements = new RunwayRequirements
                    {
                        FeederFixes = ["SMOKA"]
                    }
                }
            ],
            RunwayModes = [
                new RunwayModeConfiguration
                {
                    Identifier = "19",
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = "19L",
                            LandingRateSeconds = 180
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "19R",
                            LandingRateSeconds = 180
                        }
                    ]
                }
            ],
            Arrivals = [],
            Views = [],
            DepartureAirports = []
        };

        _scheduler = new Scheduler(
            runwayScoreCalculator,
            new AirportConfigurationProvider([_airportConfiguration]),
            Substitute.For<IPerformanceLookup>(),
            clockFixture.Instance,
            Substitute.For<ILogger>());
    }

    [Fact]
    public void WhenFlightHasRequiredFeederFix_RunwayShouldBeEligible()
    {
        var smokaFlight = new FlightBuilder("QFA1")
            .WithFeederFix("SMOKA")
            .Build();

        var gomolFlight = new FlightBuilder("QFA2")
            .WithFeederFix("GOMOL")
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(smokaFlight)
            .WithFlight(gomolFlight)
            .Build();

        _scheduler.Schedule(sequence);

        smokaFlight.AssignedRunwayIdentifier.ShouldBe("19R");
        gomolFlight.AssignedRunwayIdentifier.ShouldBe("19L");
    }

    [Fact]
    public void WhenFlightHasNoFeederFix_AllRunwaysShouldBeEligible()
    {
        var flight = new FlightBuilder("QFA1")
            .WithFeederFix(null)
            .Build();

        var sequence = new SequenceBuilder(_airportConfiguration)
            .WithFlight(flight)
            .Build();

        _scheduler.Schedule(sequence);

        flight.AssignedRunwayIdentifier.ShouldNotBeNull();
    }
}
