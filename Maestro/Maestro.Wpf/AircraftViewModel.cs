namespace Maestro.Wpf;

public class AircraftViewModel
{
    public string Callsign { get; set; } = "QFA123";

    public DateTimeOffset LandingTime { get; set; } = DateTimeOffset.Now.AddMinutes(15);

    public DateTimeOffset FeederFixTime { get; set; } = DateTime.Now.AddMinutes(5);

    public string FeederFix { get; set; } = "RIVET";
    public string? Runway { get; set; } = "16R";

    public TimeSpan TotalDelay { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RemainingDelay { get; set; } = TimeSpan.FromMinutes(5);

    public bool MaintainProfileSpeed { get; set; }
}
