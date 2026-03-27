namespace Maestro.Tools;

// Duplicated from Maestro.Core.Calculations to avoid a project reference dependency.
static class Calculations
{
    const double EarthRadiusNauticalMiles = 3440.065;

    public static double CalculateDistanceNauticalMiles(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusNauticalMiles * c;
    }

    public static double CalculateTrack(double lat1, double lon1, double lat2, double lon2)
    {
        var rlat1 = ToRadians(lat1);
        var rlon1 = ToRadians(lon1) * -1.0;
        var rlat2 = ToRadians(lat2);
        var rlon2 = ToRadians(lon2) * -1.0;

        var deltaLon = rlon1 - rlon2;
        var y = Math.Sin(deltaLon) * Math.Cos(rlat2);
        var x = Math.Cos(rlat1) * Math.Sin(rlat2) - Math.Sin(rlat1) * Math.Cos(rlat2) * Math.Cos(deltaLon);
        var trackRads = Math.Atan2(y, x) % (2.0 * Math.PI);

        if (trackRads < 0.0)
            trackRads += 2.0 * Math.PI;

        return ToDegrees(trackRads);
    }

    static double ToRadians(double degrees) => degrees * (Math.PI / 180.0);
    static double ToDegrees(double radians) => radians * (180.0 / Math.PI);
}
