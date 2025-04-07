using Maestro.Core.Dtos.Configuration;
using Maestro.Core.Model;

namespace Maestro.Core.Tests;

public class FixedSeparationProvider(TimeSpan interval) : ISeparationRuleProvider
{
    public TimeSpan Interval { get; set; } = interval;
    
    public TimeSpan GetRequiredSpacing(Flight leader, Flight trailer, RunwayModeConfigurationDto runwayMode)
    {
        return Interval;
    }
}