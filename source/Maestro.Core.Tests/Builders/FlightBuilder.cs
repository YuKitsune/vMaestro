using Maestro.Core.Model;

namespace Maestro.Core.Tests.Builders;

public class FlightBuilder(string callsign)
{
    string _aircraftType = "B738";
    AircraftCategory _aircraftCategory = AircraftCategory.Jet;
    WakeCategory _wakeCategory = WakeCategory.Medium;
    string _origin = "YMML";
    string _destination = "YSSY";
    string _feederFixIdentifier = "RIVET";
    DateTimeOffset activationTime = DateTimeOffset.Now.AddHours(-1);
    DateTimeOffset feederFixEstimate = default;
    bool manualFeederFixEstimate = false;
    DateTimeOffset feederFixTime = default;
    DateTimeOffset? passedFeederFix = null;

    DateTimeOffset landingEstimate = default;
    DateTimeOffset? targetLandingTime = null;
    DateTimeOffset landingTime = default;

    string _approachType = string.Empty;
    string _assignedRunway = "34L";

    bool _highPriority = false;

    TimeSpan? _manualDelay = null;

    State _state = State.Unstable;

    DateTimeOffset _lastSeen = default;
    bool _isFromDepartureAirport = false;
    FlightPosition? _position = null;

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

    public FlightBuilder WithAircraftCategory(AircraftCategory aircraftCategory)
    {
        _aircraftCategory = aircraftCategory;
        return this;
    }

    public FlightBuilder WithWakeCategory(WakeCategory wakeCategory)
    {
        _wakeCategory = wakeCategory;
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

    public FlightBuilder WithTargetLandingTime(DateTimeOffset targetTime)
    {
        targetLandingTime = targetTime;
        return this;
    }

    public FlightBuilder WithLandingTime(DateTimeOffset time)
    {
        landingTime = time;
        return this;
    }

    public FlightBuilder ManualDelay(TimeSpan value)
    {
        _manualDelay = value;
        return this;
    }

    public FlightBuilder HighPriority(bool value = true)
    {
        _highPriority = value;
        return this;
    }

    public FlightBuilder WithApproachType(string approachType)
    {
        _approachType = approachType;
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

    public FlightBuilder WithLastSeen(DateTimeOffset lastSeen)
    {
        _lastSeen = lastSeen;
        return this;
    }

    public FlightBuilder FromDepartureAirport(bool value = true)
    {
        _origin = "YSCB";
        _isFromDepartureAirport = value;
        return this;
    }

    public FlightBuilder WithPosition(FlightPosition position)
    {
        _position = position;
        return this;
    }

    public Flight Build()
    {
        var flight = new Flight(
            callsign,
            _destination,
            landingEstimate,
            activationTime,
            _aircraftType,
            _aircraftCategory,
            _wakeCategory)
        {
            OriginIdentifier = _origin
        };

        flight.SetApproachType(_approachType);

        flight.SetFeederFix(_feederFixIdentifier, feederFixEstimate, passedFeederFix);
        if (!string.IsNullOrEmpty(_feederFixIdentifier))
            flight.UpdateFeederFixEstimate(feederFixEstimate, manualFeederFixEstimate);

        flight.SetSequenceData(landingTime, feederFixTime, FlowControls.ProfileSpeed);

        flight.SetRunway(_assignedRunway);

        flight.UpdateLastSeen(new FixedClock(_lastSeen));

        flight.SetState(_state, new FixedClock(activationTime));

        flight.SetMaximumDelay(_manualDelay);
        flight.HighPriority = _highPriority;

        flight.Fixes =
        [
            new FixEstimate(_feederFixIdentifier, feederFixEstimate, passedFeederFix),
            new FixEstimate(_destination, landingEstimate)
        ];

        flight.IsFromDepartureAirport = _isFromDepartureAirport;

        if (_position is not null)
        {
            flight.UpdatePosition(_position);
        }

        if (targetLandingTime.HasValue)
        {
            flight.SetTargetLandingTime(targetLandingTime.Value);
        }

        return flight;
    }
}
