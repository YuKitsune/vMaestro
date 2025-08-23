namespace Maestro.Core.Model;

public class FlightPosition(
    Coordinate coordinate,
    int altitude,
    VerticalTrack verticalTrack,
    double groundSpeed,
    bool isOnGround)
{
    public Coordinate Coordinate => coordinate;
    public int Altitude => altitude;
    public VerticalTrack VerticalTrack => verticalTrack;
    public double GroundSpeed => groundSpeed;
    public bool IsOnGround => isOnGround;
}

public enum VerticalTrack
{
    Climbing,
    Maintaining,
    Descending
}
