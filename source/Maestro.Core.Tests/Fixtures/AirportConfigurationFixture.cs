using System.Text.RegularExpressions;
using Maestro.Core.Configuration;
using Maestro.Core.Model;
using Maestro.Core.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(AirportConfigurationFixture))]

namespace Maestro.Core.Tests.Fixtures;

public class AirportConfigurationFixture
{
    public AirportConfiguration Instance =>
        new()
        {
            Identifier = "YSSY",
            MinimumRadarEstimateRange = 200,
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    LandingRateSeconds = 180
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    LandingRateSeconds = 180
                }
            ],
            RunwayModes =
            [
                new RunwayMode
                {
                    Identifier = "34IVA",
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = "34L",
                            LandingRateSeconds = 180
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            LandingRateSeconds = 180
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
            RunwayAssignmentRules =
            [
                new RunwayAssignmentRule(
                    0,
                    ["RIVET", "WELSH", "BOREE", "MEPIL", "MARLN"],
                    [WakeCategory.Heavy, WakeCategory.SuperHeavy],
                    ["34L", "16R", "07", "25"]),

                new RunwayAssignmentRule(
                    1,
                    ["RIVET", "WELSH"],
                    [WakeCategory.Light, WakeCategory.Medium],
                    ["34L", "16R", "07", "25"]),

                new RunwayAssignmentRule(
                    2,
                    ["RIVET", "WELSH"],
                    [WakeCategory.Light, WakeCategory.Medium],
                    ["34R", "16L"]),

                new RunwayAssignmentRule(
                    1,
                    ["BOREE", "MEPIL", "MARLN"],
                    [WakeCategory.Light, WakeCategory.Medium],
                    ["34R", "16L", "07", "25"]),

                new RunwayAssignmentRule(
                    2,
                    ["BOREE", "MEPIL", "MARLN"],
                    [WakeCategory.Light, WakeCategory.Medium],
                    ["34L", "16R"])
            ],
            DepartureAirports = ["YSCB"]
        };
}
