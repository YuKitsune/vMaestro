using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;

namespace Maestro.Core.Model;

public interface IEstimateProvider
{
    DateTimeOffset? GetFeederFixEstimate(AirportConfiguration airportConfiguration, string feederFixIdentifier, DateTimeOffset systemEstimate, FlightPosition? flightPosition);
    DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate);
}

// TODO: Delete this and store the TTG on the flight instead
public class EstimateProvider(
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
        return systemEstimate;
    }

    public DateTimeOffset? GetLandingEstimate(Flight flight, DateTimeOffset? systemEstimate)
    {
        if (flight.FeederFixIdentifier is null ||
            flight.FeederFixEstimate is null ||
            flight.AssignedRunwayIdentifier is null)
            return systemEstimate;

        // If the flight has passed the feeder fix but vatSys didn't see it, we'll get a MaxValue for the ATO_FF
        // In this case, defer to the system estimate
        if (flight.HasPassedFeederFix && flight.ActualFeederFixTime == DateTimeOffset.MaxValue)
            return systemEstimate;

        var timeToGo = arrivalLookup.GetArrivalInterval(
            flight.DestinationIdentifier,
            flight.FeederFixIdentifier,
            flight.AssignedArrivalIdentifier,
            flight.AssignedRunwayIdentifier,
            flight.AircraftType,
            flight.AircraftCategory);

        if (timeToGo is null)
            return systemEstimate;

        // TODO: How do we actually calculate the landing time once passed the FF?

        // Once passed the feeder fix, the actual FF time is used to calculate the landing estimate
        var landingEstimate = flight.HasPassedFeederFix
            ? flight.ActualFeederFixTime?.Add(timeToGo.Value)
            : flight.FeederFixEstimate?.Add(timeToGo.Value);

        // Something went wrong, fall back to system estimate
        if (landingEstimate is null)
            return systemEstimate;

        return landingEstimate;

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
