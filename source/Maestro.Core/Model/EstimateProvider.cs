using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public interface IEstimateProvider
{
    DateTimeOffset? GetFeederFixEstimate(AirportConfiguration airportConfiguration, string feederFixIdentifier, DateTimeOffset systemEstimate, FlightPosition? flightPosition);
    DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate);
}

public class EstimateProvider(
    IPerformanceLookup performanceLookup,
    IArrivalLookup arrivalLookup,
    IFixLookup fixLookup,
    IClock clock)
    : IEstimateProvider
{
    public DateTimeOffset? GetFeederFixEstimate(
        AirportConfiguration airportConfiguration,
        string feederFixIdentifier,
        DateTimeOffset systemEstimate,
        FlightPosition? flightPosition)
    {
        if (flightPosition is null)
            return systemEstimate;

        var feederFix = fixLookup.FindFix(feederFixIdentifier);
        if (feederFix is null)
            return systemEstimate;

        var distance = Calculations.CalculateDistanceNauticalMiles(
            flightPosition.Coordinate,
            feederFix.Coordinate);

        // Prefer system estimate beyond the specified range
        if (distance > airportConfiguration.MinimumRadarEstimateRange)
            return systemEstimate;

        // For flights within range, average the radar estimate (BRL) and the system estimate
        var radarEstimate = clock.UtcNow() + TimeSpan.FromHours(distance / flightPosition.GroundSpeed);
        var difference = (radarEstimate - systemEstimate).Duration();
        var average = DateTimeOffsetHelpers.Earliest(radarEstimate, systemEstimate)
            .Add(TimeSpan.FromSeconds(difference.TotalSeconds / 2));

        return average;
    }

    public DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate)
    {
        // TODO: logging

        // We need ETA_FF in order to calculate the landing time using intervals.
        // If we don't have those, defer to the system estimate.
        if (flight.FeederFixIdentifier is null ||
            flight.EstimatedFeederFixTime is null)
            return systemEstimate;

        var aircraftPerformance = performanceLookup.GetPerformanceDataFor(flight.AircraftType);
        if (aircraftPerformance is null)
            return systemEstimate;

        var intervalToRunway = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            aircraftPerformance);
        if (intervalToRunway is null)
            return systemEstimate;

        // If the flight has passed the feeder fix but vatSys didn't see it, we'll get a MaxValue for the ATO_FF
        // In this case, defer to the system estimate
        if (flight.HasPassedFeederFix && flight.ActualFeederFixTime == DateTimeOffset.MaxValue)
            return systemEstimate;

        var feederFixTime = flight.HasPassedFeederFix
            ? flight.ActualFeederFixTime // Prefer ATO_FF if available
            : flight.EstimatedFeederFixTime; // Use ETA_FF if not passed FF

        var landingEstimateFromInterval = feederFixTime?.Add(intervalToRunway.Value);
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
