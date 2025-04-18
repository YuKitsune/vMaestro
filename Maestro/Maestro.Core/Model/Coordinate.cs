using System.Diagnostics;

namespace Maestro.Core.Model;

[DebuggerDisplay("{Latitude} {Longitude}")]
public readonly struct Coordinate(double latitude, double longitude)
{
    public double Latitude => latitude;
    public double Longitude => longitude;
}