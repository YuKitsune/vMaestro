using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Fixtures;
using RunwayPreferences = Maestro.Core.Configuration.RunwayPreferences;

[assembly: AssemblyFixture(typeof(AirportConfigurationFixture))]

namespace Maestro.Core.Tests.Fixtures;

public class AirportConfigurationFixture
{
    public TimeSpan AcceptanceRate => TimeSpan.FromSeconds(180);

    public AirportConfiguration Instance =>
        new()
        {
            Identifier = "YSSY",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                    Preferences = new RunwayPreferences
                    {
                        FeederFixes = ["RIVET", "WELSH"],
                        WakeCategories = [WakeCategory.SuperHeavy, WakeCategory.Heavy]
                    }
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                    Preferences = new RunwayPreferences
                    {
                        FeederFixes = ["BOREE", "YAKKA", "MARLN"]
                    }
                },
                new RunwayConfiguration
                {
                    Identifier = "16R",
                    LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                    Preferences = new RunwayPreferences
                    {
                        FeederFixes = ["RIVET", "WELSH"],
                        WakeCategories = [WakeCategory.SuperHeavy, WakeCategory.Heavy]
                    }
                },
                new RunwayConfiguration
                {
                    Identifier = "16L",
                    LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                    Preferences = new RunwayPreferences
                    {
                        FeederFixes = ["BOREE", "YAKKA", "MARLN"]
                    }
                }
            ],
            RunwayModes =
            [
                new RunwayModeConfiguration
                {
                    Identifier = "34IVA",
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = "34L",
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        }
                    ]
                },
                new RunwayModeConfiguration
                {
                    Identifier = "16IVA",
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = "16L",
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "16R",
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        }
                    ]
                }
            ],
            Arrivals =
            [
                new ArrivalConfiguration
                {
                    FeederFix = "RIVET",
                    Category = AircraftCategory.Jet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        { "34L", 16 },
                        { "34R", 20 },
                    }
                },
                new ArrivalConfiguration
                {
                    FeederFix = "WELSH",
                    Category = AircraftCategory.Jet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        { "34L", 16 },
                        { "34R", 20 },
                    }
                },
                new ArrivalConfiguration
                {
                    FeederFix = "BOREE",
                    Category = AircraftCategory.Jet,
                    ApproachType = "A",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        { "34L", 23 },
                        { "34R", 22 },
                    }
                },
                new ArrivalConfiguration
                {
                    FeederFix = "BOREE",
                    Category = AircraftCategory.Jet,
                    ApproachType = "P",
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        { "34L", 24 },
                        { "34R", 25 },
                    }
                }
            ],
            Views =
            [
                new ViewConfiguration
                {
                    Identifier = "BIK",
                    LeftLadder = [],
                    RightLadder = [],
                    ViewMode = ViewMode.Enroute,
                    TimeHorizonMinutes = 45
                }
            ],
            FeederFixes = ["RIVET", "WELSH", "BOREE", "YAKKA", "MARLN"],
            PreferredRunways = new Dictionary<string, string[]>
            {
                { "RIVET", ["34L", "16R"] },
                { "WELSH", ["34L", "16R"] },
                { "BOREE", ["34R", "16L"] },
                { "YAKKA", ["34R", "16L"] },
                { "MARLN", ["34R", "16L"] }
            },
            DepartureAirports =
            [
                new DepartureAirportConfiguration
                {
                    Identifier = "YSCB",
                    Distance = 50,
                    FlightTimes =
                    [
                        new DepartureAirportFlightTimeConfiguration
                        {
                            AircraftType = new AircraftCategoryConfiguration(AircraftCategory.Jet),
                            AverageFlightTime = TimeSpan.FromMinutes(30),
                        },
                        new DepartureAirportFlightTimeConfiguration
                        {
                            AircraftType = new AircraftCategoryConfiguration(AircraftCategory.NonJet),
                            AverageFlightTime = TimeSpan.FromMinutes(45),
                        }
                    ]
                }
            ]
        };
}
