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

    public static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }
}