using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public interface IEstimateProvider
{
    DateTimeOffset? GetFeederFixEstimate(Flight flight);
    DateTimeOffset? GetLandingEstimate(Flight flight);
}

public class EstimateProvider(IMaestroConfiguration configuration, IArrivalLookup arrivalLookup, IFixLookup fixLookup, IClock clock) : IEstimateProvider
{
    public DateTimeOffset? GetFeederFixEstimate(Flight flight)
    {
        var systemEstimate = flight.Estimates.LastOrDefault(f => f.FixIdentifier == flight.FeederFixIdentifier)?.Estimate;
        if (configuration.FeederFixEstimateSource == FeederFixEstimateSource.SystemEstimate)
            return systemEstimate;
        
        if (string.IsNullOrEmpty(flight.FeederFixIdentifier) || flight.LastKnownPosition is null)
            return null;
        
        var feederFix = fixLookup.FindFix(flight.FeederFixIdentifier!);
        if (feederFix is null)
            return null;
        
        var distance = Calculations.CalculateDistanceNauticalMiles(
            flight.LastKnownPosition.Coordinate,
            feederFix.Coordinate);
        
        var estimate = clock.UtcNow() + TimeSpan.FromHours(distance / flight.LastKnownPosition.GroundSpeed);
        return estimate;
    }

    public DateTimeOffset? GetLandingEstimate(Flight flight)
    {
        var systemEstimate = flight.Estimates.LastOrDefault()?.Estimate;

        // We need FF, ETA_FF, and an assigned runway in order to calculate the landing time using intervals.
        // If we don't have those, defer to the system estimate.
        if (flight.FeederFixIdentifier is null || 
            flight.EstimatedFeederFixTime is null)
            return systemEstimate;
        
        var intervalToRunway = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedRunwayIdentifier);
        if (intervalToRunway is null)
            return systemEstimate;

        var landingEstimateFromInterval = flight.EstimatedFeederFixTime.Value.Add(intervalToRunway.Value);
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