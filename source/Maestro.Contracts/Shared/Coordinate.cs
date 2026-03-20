using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

[DebuggerDisplay("{Latitude} {Longitude}")]
public readonly struct Coordinate
{
    public double Latitude { get; }
    public double Longitude { get; }

    [JsonConstructor]
    public Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}
