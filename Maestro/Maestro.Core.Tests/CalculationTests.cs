using Maestro.Core.Model;
using Shouldly;

namespace Maestro.Core.Tests;

public class CalculationTests
{
    static readonly Coordinate Melbourne = new(
        Calculations.FromDms(-37, 39, 36.5),
        Calculations.FromDms(144, 50, 31.2));
    
    static readonly Coordinate Brisbane = new(
        Calculations.FromDms(-27, 21, 57.9),
        Calculations.FromDms(153, 08, 21.2));
    
    static readonly Coordinate Rivet = new(
        Calculations.FromDms(-34, 16, 50.7),
        Calculations.FromDms(150, 25, 49.1));
    
    static readonly Coordinate Tesat = new(
        Calculations.FromDms(-33, 56, 37.7),
        Calculations.FromDms(151, 10, 57.3));

    public static IEnumerable<object[]> Distances =>
    [
        [new Coordinate(0, 0), new Coordinate(0, 0), 0],
        [Melbourne, Tesat, 381.1],
        [Brisbane, Tesat, 406.5],
        [Rivet, Tesat, 42.6],
    ];
    
    [Theory]
    [MemberData(nameof(Distances))]
    public void CalculateDistanceNauticalMiles_ShouldReturnExpectedResult(
        Coordinate p1,
        Coordinate p2,
        double expectedDistance)
    {
        var distance = Calculations.CalculateDistanceNauticalMiles(p1, p2);
        distance.ShouldBe(expectedDistance, tolerance: 2);
        
        var distanceReversed = Calculations.CalculateDistanceNauticalMiles(p2, p1);
        distanceReversed.ShouldBe(expectedDistance, tolerance: 2);
    }

    [Theory]
    [InlineData(0 ,0, 1, 0, 0)]
    [InlineData(0 ,0, 1, 1, 45)]
    [InlineData(0 ,0, 0, 1, 90)]
    [InlineData(0 ,0, -1, 1, 135)]
    [InlineData(0 ,0, -1, 0, 180)]
    [InlineData(0 ,0, -1, -1, 225)]
    [InlineData(0 ,0, 0, -1, 270)]
    [InlineData(0 ,0, 1, -1, 315)]
    public void CalculateTrack_ShouldReturnExpectedResult(
        double lat1,
        double lon1,
        double lat2,
        double lon2,
        double expectedTrack)
    {
        var p1 = new Coordinate(lat1, lon1);
        var p2 = new Coordinate(lat2, lon2);
        
        var track = Calculations.CalculateTrack(p1, p2);
        track.ShouldBe(expectedTrack, tolerance: 0.5);
    }
}