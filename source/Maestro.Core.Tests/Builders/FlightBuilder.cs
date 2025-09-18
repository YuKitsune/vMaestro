using Maestro.Core.Model;

namespace Maestro.Core.Tests.Builders;

public class FlightBuilder(string callsign)
{
    string _aircraftType = "B738";
    WakeCategory _wakeCategory = WakeCategory.Medium;
    string _origin = "YMML";
    string _destination = "YSSY";
    string _feederFixIdentifier = "RIVET";
    DateTimeOffset estimatedTimeOfDeparture = DateTimeOffset.Now;
    TimeSpan? _estimatedFlightTime;
    DateTimeOffset activationTime = DateTimeOffset.Now.AddHours(-1);
    DateTimeOffset feederFixEstimate = DateTimeOffset.Now;
    bool manualFeederFixEstimate = false;
    DateTimeOffset feederFixTime = default;
    DateTimeOffset? passedFeederFix = null;

    DateTimeOffset landingEstimate = DateTimeOffset.Now;
    DateTimeOffset landingTime = default;
    bool manualLandingTime = false;

    string _assignedArrival = "RIVET4";
    string _assignedRunway = "34L";
    bool _manualRunway = false;

    bool _noDelay = false;
    bool _highPriority = false;

    State _state = State.Unstable;

    DateTimeOffset _lastSeen = DateTimeOffset.Now;

    public FlightBuilder WithActivationTime(DateTimeOffset time)
    {
        activationTime = time;
        return this;
    }

    public FlightBuilder WithAircraftType(string aircraftType)
    {
        _aircraftType = aircraftType;
        return this;
    }

    public FlightBuilder WithFeederFix(string? feederFixIdentifier)
    {
        _feederFixIdentifier = feederFixIdentifier ?? string.Empty;
        return this;
    }

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate, bool manual = false)
    {
        feederFixEstimate = estimate;
        manualFeederFixEstimate = manual;
        return this;
    }

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate, TimeSpan arrivalInterval, bool manual = false)
    {
        feederFixEstimate = estimate;
        manualFeederFixEstimate = manual;
        landingEstimate = estimate + arrivalInterval;
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

    public FlightBuilder HighPriority(bool value = true)
    {
        _highPriority = value;
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

    public FlightBuilder WithEstimatedDeparture(DateTimeOffset estimatedDeparture)
    {
        estimatedTimeOfDeparture = estimatedDeparture;
        return this;
    }

    public FlightBuilder WithEstimatedFlightTime(TimeSpan estimatedFlightTime)
    {
        _estimatedFlightTime = estimatedFlightTime;
        return this;
    }

    public Flight Build()
    {
        var flight = new Flight(callsign, _destination, landingEstimate, activationTime)
        {
            AircraftType = _aircraftType,
            WakeCategory = _wakeCategory,
            OriginIdentifier = _origin,
            EstimatedDepartureTime = estimatedTimeOfDeparture,
            EstimatedTimeEnroute = _estimatedFlightTime,
            AssignedArrivalIdentifier = _assignedArrival
        };

        flight.SetFeederFix(_feederFixIdentifier, feederFixEstimate, passedFeederFix);
        if (!string.IsNullOrEmpty(_feederFixIdentifier))
            flight.UpdateFeederFixEstimate(feederFixEstimate, manualFeederFixEstimate);

        if (feederFixTime != default)
            flight.SetFeederFixTime(feederFixTime);

        if (landingTime != default)
            flight.SetLandingTime(landingTime, manualLandingTime);

        if (landingTime == default)
            flight.SetLandingTime(landingEstimate);

        flight.SetRunway(_assignedRunway, _manualRunway);

        flight.UpdateLastSeen(new FixedClock(_lastSeen));

        flight.SetState(_state, new FixedClock(activationTime));

        flight.NoDelay = _noDelay;
        flight.HighPriority = _highPriority;

        flight.Fixes =
        [
            new FixEstimate(_feederFixIdentifier, feederFixEstimate, passedFeederFix),
            new FixEstimate(_destination, landingEstimate)
        ];

        flight.ResetInitialEstimates();

        return flight;
    }
}
