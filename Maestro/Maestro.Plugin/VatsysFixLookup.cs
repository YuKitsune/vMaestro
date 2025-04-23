using Maestro.Core.Model;
using vatsys;
using Coordinate = Maestro.Core.Model.Coordinate;

namespace Maestro.Plugin;

public class VatsysFixLookup : IFixLookup
{
    public Fix? FindFix(string identifier)
    {
        var intersection = Airspace2.GetIntersection(identifier);
        if (intersection is not null)
            return new Fix(
                intersection.Name,
                new Coordinate(intersection.LatLong.Latitude, intersection.LatLong.Longitude));

        // For simplicity, we're treating airports as fixes
        var airport = Airspace2.GetAirport(identifier);
        if (airport is not null)
            return new Fix(
                airport.ICAOName,
                new Coordinate(airport.LatLong.Latitude, airport.LatLong.Longitude));
            
        return null;
    }
}