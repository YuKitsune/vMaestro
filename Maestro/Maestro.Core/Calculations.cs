using Maestro.Core.Model;

namespace Maestro.Core;

public static class Calculations
{
    public static double FromDms(int degrees, int minutes, double seconds)
    {
        var isNegative = degrees < 0;
        var decimalDegrees = Math.Abs(degrees) + (minutes / 60d) + (seconds / 3600d);
        return isNegative ? -decimalDegrees : decimalDegrees;
    }
    
    public static double CalculateDistanceNauticalMiles(Coordinate point1, Coordinate point2)
    {
        const double earthRadiusNauticalMiles = 3440.065; // Mean Earth radius in nautical miles

        var dLat = DegreesToRadians(point2.Latitude - point1.Latitude);
        var dLon = DegreesToRadians(point2.Longitude - point1.Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(point1.Latitude)) * Math.Cos(DegreesToRadians(point2.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusNauticalMiles * c;
    }

    public static double CalculateTrack(Coordinate point1, Coordinate point2)
    {
        var lat1 = DegreesToRadians(point1.Latitude);
        var lon1 = DegreesToRadians(point1.Longitude) * -1.0;
        var lat2 = DegreesToRadians(point2.Latitude);
        var lon2 = DegreesToRadians(point2.Longitude) * -1.0;

        var deltaLon = lon1 - lon2;
        var y = Math.Sin(deltaLon) * Math.Cos(lat2);
        var x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(deltaLon);
        var trackRads = Math.Atan2(y, x) % (2.0 * Math.PI);

        if (trackRads < 0.0)
            trackRads += 2.0 * Math.PI;

        return RadiansToDegrees(trackRads);
    }

    static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
    
    static double RadiansToDegrees(double radians) => radians * (180.0 / Math.PI);
}