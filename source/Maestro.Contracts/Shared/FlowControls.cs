using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FlowControls
{
    HighSpeed,
    ReduceSpeed
}
