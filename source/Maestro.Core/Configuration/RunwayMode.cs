using Maestro.Core.Handlers;
using Newtonsoft.Json;

namespace Maestro.Core.Configuration;

public class RunwayMode
{
    public RunwayMode(RunwayModeDto dto)
    {
        Identifier = dto.Identifier;
        Runways = dto.Runways.Select(r =>
                new RunwayConfiguration
                {
                    Identifier = r.RunwayIdentifier,
                    LandingRateSeconds = r.AcceptanceRate
                })
            .ToArray();
    }

    [JsonConstructor]
    public RunwayMode(string identifier, RunwayConfiguration[] runways)
    {
        Identifier = identifier;
        Runways = runways;
    }

    public string Identifier { get; private set; }
    public RunwayConfiguration[] Runways { get; private set; }
}
