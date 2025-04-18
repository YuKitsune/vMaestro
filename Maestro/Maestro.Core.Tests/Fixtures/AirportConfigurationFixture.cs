using Maestro.Core.Configuration;
using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(AirportConfigurationFixture))]

namespace Maestro.Core.Tests.Fixtures;

public class AirportConfigurationFixture
{
    public AirportConfiguration Instance =>
        new AirportConfiguration
        {
            Identifier = "YSSY",
            Runways =
            [
                new RunwayConfiguration
                {
                    Identifier = "34L",
                    DefaultLandingRateSeconds = 180
                },
                new RunwayConfiguration
                {
                    Identifier = "34R",
                    DefaultLandingRateSeconds = 180
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
                            DefaultLandingRateSeconds = 180
                        },
                        new RunwayConfiguration
                        {
                            Identifier = "34R",
                            DefaultLandingRateSeconds = 180
                        }
                    ]
                }
            ],
            Arrivals =
            [
                new ArrivalConfiguration
                {
                    ArrivalIdentifier = "RIVET4",
                    FeederFix = "RIVET",
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
                    LeftLadderConfiguration = null,
                    RightLadderConfiguration = null,
                    LadderReferenceTime = LadderReferenceTime.FeederFixTime
                }
            ],
            FeederFixes = ["RIVET"]
        };
}