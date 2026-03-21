using System.Diagnostics;
using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Shared;

[DebuggerDisplay("{Latitude} {Longitude}")]
[MessagePackObject]
public readonly struct Coordinate
{
    [Key(0)]
    public double Latitude { get; }

    [Key(1)]
    public double Longitude { get; }

    [JsonConstructor]
    [SerializationConstructor]
    public Coordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}
