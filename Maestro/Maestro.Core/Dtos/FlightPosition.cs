namespace Maestro.Core.Dtos;

public readonly struct FlightPosition(double latitude, double longitude, int altitude)
{
    public double Latitude => latitude;
    public double Longitude => longitude;
    public int Altitude => altitude;
    
    public Coordinate ToCoordinate() => new(Longitude, Latitude);
}