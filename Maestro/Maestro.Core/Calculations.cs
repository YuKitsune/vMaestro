using Maestro.Core.Dtos;

namespace Maestro.Core;

public static class Calculations
{
    public static double CalculateDistanceNauticalMiles(Coordinate point1, Coordinate point2)
    {
        var lat1Radians = DegreesToRadians(point1.Latitude);
        var lon1Radians = DegreesToRadians(point1.Longitude);
        var lat2Radians = DegreesToRadians(point2.Latitude);
        var lon2Radians = DegreesToRadians(point2.Longitude);
        
        var dLat = lat2Radians - lat1Radians;
        var dLon = lon2Radians - lon1Radians;
        
        var num1 = Math.Pow(Math.Sin(dLat / 2.0), 2.0);
        var num2 = Math.Pow(Math.Sin(dLon / 2.0), 2.0);
        return 2.0 * Math.Asin(Math.Sqrt(num1 + Math.Cos(lat1Radians) * Math.Cos(lat2Radians) * num2)) * 10800.0 / Math.PI;
    }

    public static double CalculateTrack(Coordinate point1, Coordinate point2)
    {
        var lat1Radians = DegreesToRadians(point1.Latitude);
        var lon1Radians = DegreesToRadians(point1.Longitude * -1);
        var lat2Radians = DegreesToRadians(point2.Latitude);
        var lon2Radians = DegreesToRadians(point2.Longitude * -1);
        
        var trackRads = Math.Atan2(Math.Sin(lon1Radians - lon2Radians) * Math.Cos(lat1Radians), Math.Cos(lat1Radians) * Math.Sin(lat2Radians) - Math.Sin(lat1Radians) * Math.Cos(lat2Radians) * Math.Cos(lon1Radians - lon2Radians)) % (2.0 * Math.PI);
        if (trackRads < 0.0)
            trackRads += 2.0 * Math.PI;
        
        return RadiansToDegrees(trackRads);
    }
    
    public static double DegreesToRadians(double degrees) => degrees / 180.0 * Math.PI;

    public static double RadiansToDegrees(double rads) => rads * 180.0 / Math.PI;
}