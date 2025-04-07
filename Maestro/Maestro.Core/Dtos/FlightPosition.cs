namespace Maestro.Core.Dtos;

public readonly struct FlightPosition(
    double latitude,
    double longitude,
    int altitude,
    VerticalTrack verticalTrack,
    double groundSpeed)
{
    public double Latitude => latitude;
    public double Longitude => longitude;
    public int Altitude => altitude;
    public VerticalTrack VerticalTrack => verticalTrack;
    public double GroundSpeed => groundSpeed;
    
    public Coordinate ToCoordinate() => new(Longitude, Latitude);
}

public enum VerticalTrack
{
    Climbing,
    Maintaining,
    Descending
}