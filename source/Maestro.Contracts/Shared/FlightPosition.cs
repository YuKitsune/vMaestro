using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Shared;

[MessagePackObject]
public class FlightPosition
{
    [Key(0)]
    public Coordinate Coordinate { get; }

    [Key(1)]
    public int Altitude { get; }

    [Key(2)]
    public VerticalTrack VerticalTrack { get; }

    [Key(3)]
    public double GroundSpeed { get; }

    [Key(4)]
    public bool IsOnGround { get; }

    [JsonConstructor]
    [SerializationConstructor]
    public FlightPosition(
        Coordinate coordinate,
        int altitude,
        VerticalTrack verticalTrack,
        double groundSpeed,
        bool isOnGround)
    {
        Coordinate = coordinate;
        Altitude = altitude;
        VerticalTrack = verticalTrack;
        GroundSpeed = groundSpeed;
        IsOnGround = isOnGround;
    }
}
