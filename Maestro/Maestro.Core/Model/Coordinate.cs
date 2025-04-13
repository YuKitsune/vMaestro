namespace Maestro.Core.Model;

public readonly struct Coordinate(double latitude, double longitude)
{
    public double Latitude => latitude;
    public double Longitude => longitude;
}