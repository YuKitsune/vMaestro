using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(AirportConfigurationFixture))]

namespace Maestro.Core.Tests.Fixtures;

public class AirportConfigurationFixture
{
    public TimeSpan AcceptanceRate => TimeSpan.FromSeconds(180);

    public AirportConfiguration Instance =>
        new()
        {
            Identifier = "YSSY",
            Runways = [ "34L", "34R", "16L", "16R"],
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
                            ApproachType = string.Empty,
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                            FeederFixes = ["RIVET", "WELSH"]
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            ApproachType = string.Empty,
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                            FeederFixes = ["BOREE", "YAKKA", "MARLN"]
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
                            ApproachType = string.Empty,
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                            FeederFixes = ["BOREE", "YAKKA", "MARLN"]
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "16R",
                            ApproachType = string.Empty,
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds,
                            FeederFixes = ["RIVET", "WELSH"]
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
                            Aircraft = new AircraftCategoryDescriptor(AircraftCategory.Jet),
                            AverageFlightTime = TimeSpan.FromMinutes(30),
                        },
                        new DepartureAirportFlightTimeConfiguration
                        {
                            Aircraft = new AircraftCategoryDescriptor(AircraftCategory.NonJet),
                            AverageFlightTime = TimeSpan.FromMinutes(45),
                        }
                    ]
                }
            ]
        };
}
