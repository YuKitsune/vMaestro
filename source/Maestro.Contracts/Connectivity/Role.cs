using System.Text.Json.Serialization;

namespace Maestro.Contracts.Connectivity;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Role
{
    Flow,
    Enroute,
    Approach,
    Observer
}
