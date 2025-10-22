using System.Text.RegularExpressions;
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
                }
            ],
            Arrivals =
            [
                new ArrivalConfiguration
                {
                    FeederFix = "RIVET",
                    ArrivalRegex = new Regex(@"RIVET\d"),
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
                    ArrivalRegex = new Regex(@"ODALE\d"),
                    Category = AircraftCategory.Jet,
                    RunwayIntervals = new Dictionary<string, int>
                    {
                        { "34L", 16 },
                        { "34R", 20 },
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
                    ViewMode = ViewMode.Enroute
                }
            ],
            FeederFixes = ["RIVET", "WELSH"],
            PreferredRunways = [],
            DepartureAirports = ["YSCB"]
        };
}
