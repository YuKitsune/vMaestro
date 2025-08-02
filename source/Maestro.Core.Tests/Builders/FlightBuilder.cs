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
    DateTimeOffset passedFeederFix = default;

    DateTimeOffset landingEstimate = DateTimeOffset.Now;
    DateTimeOffset landingTime = default;
    bool manualLandingTime = false;

    string _assignedArrival = "RIVET4";
    string _assignedRunway = "34L";
    bool _manualRunway = false;

    bool _noDelay = false;

    State _state = State.Unstable;

    DateTimeOffset _lastSeen = DateTimeOffset.Now;

    public FlightBuilder WithAircraftType(string aircraftType)
    {
        _aircraftType = aircraftType;
        return this;
    }

    public FlightBuilder WithFeederFix(string feederFixIdentifier)
    {
        _feederFixIdentifier = feederFixIdentifier;
        return this;
    }

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

    public FlightBuilder PassedFeederFixAt(DateTimeOffset time)
    {
        passedFeederFix = time;
        return this;
    }

    public FlightBuilder WithLandingEstimate(DateTimeOffset estimate)
    {
        landingEstimate = estimate;
        return this;
    }

    public FlightBuilder WithLandingTime(DateTimeOffset time, bool manual = false)
    {
        landingTime = time;
        manualLandingTime = manual;
        return this;
    }

    public FlightBuilder NoDelay(bool value = true)
    {
        _noDelay = value;
        return this;
    }

    public FlightBuilder WithArrival(string arrivalIdentifier)
    {
        _assignedArrival = arrivalIdentifier;
        return this;
    }

    public FlightBuilder WithRunway(string runway, bool manual = false)
    {
        _assignedRunway = runway;
        _manualRunway = manual;
        return this;
    }

    public FlightBuilder WithState(State state)
    {
        _state = state;
        return this;
    }

    public FlightBuilder WithLastSeen(DateTimeOffset lastSeen)
    {
        _lastSeen = lastSeen;
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
            feederFix,
            landingEstimate);

        if (feederFixTime != default)
            flight.SetFeederFixTime(feederFixTime);

        if (passedFeederFix != default)
            flight.PassedFeederFix(passedFeederFix);

        if (landingTime != default)
            flight.SetLandingTime(landingTime, manualLandingTime);

        flight.SetArrival(_assignedArrival);
        flight.SetRunway(_assignedRunway, _manualRunway);

        flight.Activate(new FixedClock(DateTimeOffset.Now));

        flight.UpdateLastSeen(new FixedClock(_lastSeen));

        flight.SetState(_state);

        flight.NoDelay = _noDelay;

        return flight;
    }
}
