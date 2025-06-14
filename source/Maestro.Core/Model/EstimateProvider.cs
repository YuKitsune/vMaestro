using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public interface IEstimateProvider
{
    DateTimeOffset? GetFeederFixEstimate(string? feederFixIdentifier, DateTimeOffset? systemEstimate, FlightPosition? flightPosition);
    DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate);
}

public class EstimateProvider(IMaestroConfiguration configuration, IPerformanceLookup performanceLookup, IArrivalLookup arrivalLookup, IFixLookup fixLookup, IClock clock)
    : IEstimateProvider
{
    public DateTimeOffset? GetFeederFixEstimate(string? feederFixIdentifier, DateTimeOffset? systemEstimate, FlightPosition? flightPosition)
    {
        if (configuration.FeederFixEstimateSource == FeederFixEstimateSource.SystemEstimate ||
            string.IsNullOrEmpty(feederFixIdentifier) ||
            flightPosition is null)
            return systemEstimate;

        var feederFix = fixLookup.FindFix(feederFixIdentifier!);
        if (feederFix is null)
            return systemEstimate;

        // BRL method
        var distance = Calculations.CalculateDistanceNauticalMiles(
            flightPosition.Coordinate,
            feederFix.Coordinate);
        
        var estimate = clock.UtcNow() + TimeSpan.FromHours(distance / flightPosition.GroundSpeed);
        return estimate;
    }

    public DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate)
    {
        // We need ETA_FF in order to calculate the landing time using intervals.
        // If we don't have those, defer to the system estimate.
        if (flight.FeederFixIdentifier is null || 
            flight.EstimatedFeederFixTime is null)
            return systemEstimate;

        var aircraftPerformance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        var intervalToRunway = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            aircraftPerformance?.Type ?? AircraftType.Jet);
        if (intervalToRunway is null)
            return systemEstimate;

        var feederFixTime = flight.HasPassedFeederFix
            ? flight.ActualFeederFixTime!.Value
            : flight.EstimatedFeederFixTime!.Value;
        var landingEstimateFromInterval = feederFixTime.Add(intervalToRunway.Value);

        return landingEstimateFromInterval;

        // TODO: Calculate landing estimate based on flight trajectory
        // var performanceData = _performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        // var maxDescentSpeed = performanceData.GetDescentSpeedAt(20000);
        //
        // var takeDistance = 0d;
        // var i = 1;
        // var eet = TimeSpan.Zero;
        // while (takeDistance < trackMiles && i < flight.Trajectory.Length)
        // {
        //     var lastPos = flight.Trajectory[flight.Trajectory.Length - i];
        //     var nextPos = flight.Trajectory[flight.Trajectory.Length - (++i)];
        //
        //     takeDistance += Calculations.CalculateDistanceNauticalMiles(lastPos.Position, nextPos.Position);
        //     eet = eet.Add(lastPos.Interval);
        //
        //     if (!performanceData.IsJet)
        //         continue;
        //
        //     switch(flight.FlowControls)
        //     {
        //         case FlowControls.MaxSpeed:
        //             if(lastPos.Altitude > 6000)//increase speeds to cancel 250 knot restriction
        //             {
        //                 var profileSpeed = performanceData.GetDescentSpeedAt(lastPos.Altitude);
        //                 if(maxDescentSpeed > profileSpeed)
        //                 {
        //                     var ratio = maxDescentSpeed / profileSpeed;
        //                     var seconds = lastPos.Interval.TotalSeconds - (lastPos.Interval.TotalSeconds * ratio);
        //                     eet = eet.Add(TimeSpan.FromSeconds(seconds)); // remove the difference
        //                 }
        //             }
        //             break;
        //             
        //         case FlowControls.S250:
        //             if(lastPos.Altitude > 10000)
        //             {
        //                 var profileSpeed = performanceData.GetDescentSpeedAt(lastPos.Altitude);
        //                 if(profileSpeed > 250)
        //                 {
        //                     var ratio = profileSpeed / 250.0;
        //                     var seconds = (lastPos.Interval.TotalSeconds * ratio) - lastPos.Interval.TotalSeconds;
        //                     eet = eet.Add(TimeSpan.FromSeconds(seconds));
        //                 }
        //             }
        //             break;
        //     }
        // }
    }
}