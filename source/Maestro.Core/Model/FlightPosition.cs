namespace Maestro.Core.Model;

public class FlightPosition(
    Coordinate coordinate,
    int altitude,
    VerticalTrack verticalTrack,
    double groundSpeed)
{
    public Coordinate Coordinate => coordinate;
    public int Altitude => altitude;
    public VerticalTrack VerticalTrack => verticalTrack;
    public double GroundSpeed => groundSpeed;
}

public enum VerticalTrack
{
    Climbing,
    Maintaining,
    Descending
}
