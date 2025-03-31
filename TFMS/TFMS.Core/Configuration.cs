namespace TFMS.Core;

public class Configuration
{
    public static Configuration Demo = new Configuration();

    public TimeSpan UnstableWindow { get; set; } = TimeSpan.FromMinutes(60);

    public TimeSpan StableWindow { get; set; } = TimeSpan.FromMinutes(40);

    public Airport[] Airports { get; set; } =
    [
        new Airport
        {
            Identifier = "YSSY",
            Runways =
            [
                new Runway{ Identifier = "16L" },
                new Runway{ Identifier = "34L" },
                new Runway{ Identifier = "07" },
                new Runway{ Identifier = "16R" },
                new Runway{ Identifier = "34R" },
                new Runway{ Identifier = "25" },
            ],
            RunwayModes =
            [
                new RunwayMode
                { 
                    Identifier = "34IVA",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "34L", LandingRate = TimeSpan.FromSeconds(180) },
                        new RunwayRate{ RunwayIdentifier = "34R", LandingRate = TimeSpan.FromSeconds(180) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "34I",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "34L", LandingRate = TimeSpan.FromSeconds(210) },
                        new RunwayRate{ RunwayIdentifier = "34R", LandingRate = TimeSpan.FromSeconds(210) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "16IVA",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "16R", LandingRate = TimeSpan.FromSeconds(180) },
                        new RunwayRate{ RunwayIdentifier = "16L", LandingRate = TimeSpan.FromSeconds(180) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "16I",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "16R", LandingRate = TimeSpan.FromSeconds(210) },
                        new RunwayRate{ RunwayIdentifier = "16L", LandingRate = TimeSpan.FromSeconds(210) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "25I",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "25", LandingRate = TimeSpan.FromSeconds(210) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "07I",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "07", LandingRate = TimeSpan.FromSeconds(210) }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "SODPROPS",
                    RunwayRates =
                    [
                        new RunwayRate{ RunwayIdentifier = "34L", LandingRate = TimeSpan.FromSeconds(300) }
                    ]
                }
            ],
            Sectors =
            [
                new Sector
                {
                    Identifier = "BIK/GLB",
                    Fixes = ["RIVET", "WELSH"]
                },
                new Sector
                {
                    Identifier = "ARL",
                    Fixes = ["BOREE", "YAKKA"]
                },
                new Sector
                {
                    Identifier = "OCN",
                    Fixes = ["MARLN"]
                },
                new Sector
                {
                    Identifier = "ALL",
                    Fixes = ["RIVET", "WELSH", "BOREE", "YAKKA", "MARLN"]
                }
            ],
            FeederFixes = ["RIVET", "WELSH", "BOREE", "YAKKA", "MARLN"]
        }
    ];
}

public class Airport
{
    public string Identifier { get; set; }
    public Runway[] Runways { get; set; }
    public RunwayMode[] RunwayModes { get; set; }
    public Sector[] Sectors { get; set; }
    public string[] FeederFixes { get; set; }
}

public class Runway
{
    public string Identifier { get; set; }
    public TimeSpan? DefaultLandingRate { get; set; }
}

public class RunwayMode
{
    public string Identifier { get; set; }
    public RunwayRate[] RunwayRates { get; set; }
}

public class RunwayRate
{
    public string RunwayIdentifier { get; set; }
    public TimeSpan LandingRate { get; set; }
}

public class Sector
{
    public string Identifier { get; set; }
    public string[] Fixes { get; set; }
}
