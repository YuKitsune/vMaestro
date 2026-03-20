using System.Text.Json.Serialization;

namespace Maestro.Contracts.Shared;

public class FlightPosition
{
    public Coordinate Coordinate { get; }
    public int Altitude { get; }
    public VerticalTrack VerticalTrack { get; }
    public double GroundSpeed { get; }
    public bool IsOnGround { get; }

    [JsonConstructor]
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
