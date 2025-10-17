using Maestro.Core.Configuration;
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
            MinimumRadarEstimateRange = 200,
            Runways = ["34L", "34R"],
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
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            ApproachType = string.Empty,
                            LandingRateSeconds = (int)AcceptanceRate.TotalSeconds
                        }
                    ]
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
            DepartureAirports = ["YSCB"]
        };
}
