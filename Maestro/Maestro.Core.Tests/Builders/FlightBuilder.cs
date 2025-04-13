using Maestro.Core.Model;

namespace Maestro.Core.Tests.Builders;

public class FlightBuilder(string callsign)
{
    string _callsign = callsign;
    string _aircraftType = "B738";
    WakeCategory _wakeCategory = WakeCategory.Medium;
    string _origin = "YMML";
    string _destination = "YSSY";
    string _feederFixIdentifier = "RIVET";
    DateTimeOffset feederFixEstimate = DateTimeOffset.Now;
    DateTimeOffset landingEstimate = DateTimeOffset.Now;

    string _assignedRunway = "34L";

    State _state = State.Unstable;

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate)
    {
        feederFixEstimate = estimate;
        return this;
    }

    public FlightBuilder WithLandingEstimate(DateTimeOffset estimate)
    {
        landingEstimate = estimate;
        return this;
    }

    public FlightBuilder WithRunway(string runway)
    {
        _assignedRunway = runway;
        return this;
    }

    public FlightBuilder WithState(State state)
    {
        _state = state;
        return this;
    }
    
    public Flight Build()
    {
        var flight = new Flight
        {
            Callsign = _callsign,
            AircraftType = _aircraftType,
            WakeCategory = _wakeCategory,
            OriginIdentifier = _origin,
            DestinationIdentifier = _destination,
            FeederFixIdentifier = _feederFixIdentifier,
            AssignedRunwayIdentifier = _assignedRunway
        };
        
        flight.UpdateFeederFixEstimate(feederFixEstimate);
        flight.UpdateLandingEstimate(landingEstimate);
        flight.SetState(_state);

        return flight;
    }
}