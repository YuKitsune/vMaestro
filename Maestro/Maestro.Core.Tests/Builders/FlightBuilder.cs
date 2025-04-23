using Maestro.Core.Model;

namespace Maestro.Core.Tests.Builders;

public class FlightBuilder(string callsign)
{
    string _aircraftType = "B738";
    WakeCategory _wakeCategory = WakeCategory.Medium;
    string _origin = "YMML";
    string _destination = "YSSY";
    string _feederFixIdentifier = "RIVET";
    DateTimeOffset feederFixEstimate = DateTimeOffset.Now;
    DateTimeOffset feederFixTime = default;
    DateTimeOffset landingEstimate = DateTimeOffset.Now;
    DateTimeOffset landingTime = default;

    string _assignedRunway = "34L";

    State _state = State.Unstable;

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate)
    {
        feederFixEstimate = estimate;
        return this;
    }

    public FlightBuilder WithFeederFixTime(DateTimeOffset time)
    {
        feederFixTime = time;
        return this;
    }

    public FlightBuilder WithLandingEstimate(DateTimeOffset estimate)
    {
        landingEstimate = estimate;
        return this;
    }

    public FlightBuilder WithLandingTime(DateTimeOffset time)
    {
        landingTime = time;
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
        var feederFix = !string.IsNullOrEmpty(_feederFixIdentifier)
            ? new FixEstimate(_feederFixIdentifier, feederFixEstimate)
            : null;

        var flight = new Flight(
            callsign,
            _aircraftType,
            _wakeCategory,
            _origin,
            _destination,
            _assignedRunway,
            feederFix,
            landingEstimate);
        
        if (feederFixTime != default)
            flight.SetFeederFixTime(feederFixTime);
        
        if (landingTime != default)
            flight.SetLandingTime(landingTime);
        
        flight.Activate(new FixedClock(DateTimeOffset.Now));
        
        flight.SetState(_state);

        return flight;
    }
}