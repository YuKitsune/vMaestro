using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum VerticalTrack
{
    Climbing,
    Maintaining,
    Descending
}
