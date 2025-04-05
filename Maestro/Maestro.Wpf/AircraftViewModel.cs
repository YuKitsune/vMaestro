using Maestro.Core.Model;

namespace Maestro.Wpf;

public class AircraftViewModel
{
    public string Callsign { get; set; } = "QFA123";

    public DateTimeOffset LandingTime { get; set; } = DateTimeOffset.UtcNow.AddMinutes(15);

    public DateTimeOffset? FeederFixTime { get; set; } = DateTimeOffset.UtcNow.AddMinutes(5);

    public string? FeederFix { get; set; } = "RIVET";
    public string? Runway { get; set; } = "34L";

    public TimeSpan TotalDelay { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RemainingDelay { get; set; } = TimeSpan.FromMinutes(5);
}
