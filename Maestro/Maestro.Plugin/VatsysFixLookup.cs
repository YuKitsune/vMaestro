using Maestro.Core.Model;
using vatsys;
using Coordinate = Maestro.Core.Model.Coordinate;

namespace Maestro.Plugin;

public class VatsysFixLookup : IFixLookup
{
    public Fix? FindFix(string identifier)
    {
        var intersection = Airspace2.GetIntersection(identifier);
        if (intersection is null)
            return null;

        return new Fix(
            intersection.Name,
            new Coordinate(intersection.LatLong.Latitude, intersection.LatLong.Longitude));
    }
}