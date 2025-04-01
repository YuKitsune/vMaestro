namespace TFMS.Core.Configuration;

public class MaestroConfiguration
{
    //public static MaestroConfiguration Demo = new MaestroConfiguration();

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
                    Runways =
                    [
                        new Runway { Identifier = "34L", DefaultLandingRateSeconds = 180 },
                        new Runway { Identifier = "34R", DefaultLandingRateSeconds = 180 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "34I",
                    Runways =
                    [
                        new Runway { Identifier = "34L", DefaultLandingRateSeconds = 210 },
                        new Runway { Identifier = "34R", DefaultLandingRateSeconds = 210 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "16IVA",
                    Runways =
                    [
                        new Runway { Identifier = "16R", DefaultLandingRateSeconds = 180 },
                        new Runway { Identifier = "16L", DefaultLandingRateSeconds = 180 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "16I",
                    Runways =
                    [
                        new Runway { Identifier = "16R", DefaultLandingRateSeconds = 210 },
                        new Runway { Identifier = "16L", DefaultLandingRateSeconds = 210 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "25I",
                    Runways =
                    [
                        new Runway { Identifier = "25", DefaultLandingRateSeconds = 210 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "07I",
                    Runways =
                    [
                        new Runway { Identifier = "07", DefaultLandingRateSeconds = 210 }
                    ]
                },
                new RunwayMode
                {
                    Identifier = "SODPROPS",
                    Runways =
                    [
                        new Runway { Identifier = "34L", DefaultLandingRateSeconds = 300 }
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
    public int DefaultLandingRateSeconds { get; set; }
}

public class RunwayMode
{
    public string Identifier { get; set; }
    public Runway[] Runways { get; set; }
}

public class Sector
{
    public string Identifier { get; set; }
    public string[] Fixes { get; set; }
}
