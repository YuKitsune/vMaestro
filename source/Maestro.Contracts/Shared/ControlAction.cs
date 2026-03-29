using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlAction
{
    Expedite,
    NoDelay,
    Resume,
    SpeedReduction,
    PathStretching,
    Holding
}
