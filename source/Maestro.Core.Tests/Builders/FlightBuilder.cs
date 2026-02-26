using Maestro.Core.Model;

namespace Maestro.Core.Tests.Builders;

public class FlightBuilder(string callsign)
{
    string _aircraftType = "B738";
    AircraftCategory _aircraftCategory = AircraftCategory.Jet;
    WakeCategory _wakeCategory = WakeCategory.Medium;
    string _origin = "YMML";
    string _destination = "YSSY";
    string? _feederFixIdentifier = "RIVET";
    DateTimeOffset _activationTime = DateTimeOffset.Now.AddHours(-1);
    DateTimeOffset _feederFixEstimate = default;
    bool _manualFeederFixEstimate = false;
    DateTimeOffset _feederFixTime = default;
    DateTimeOffset? _passedFeederFix = null;

    DateTimeOffset _landingEstimate = default;
    DateTimeOffset? _targetLandingTime = null;
    DateTimeOffset _landingTime = default;

    string _approachType = string.Empty;
    string _assignedRunway = "34L";

    bool _highPriority = false;

    TimeSpan? _manualDelay = null;

    State _state = State.Unstable;

    DateTimeOffset _lastSeen = default;
    bool _isFromDepartureAirport = false;
    FlightPosition? _position = null;
    TimeSpan _timeToGo = TimeSpan.FromMinutes(20);
    bool _isManuallyInserted = false;

    public FlightBuilder WithActivationTime(DateTimeOffset time)
    {
        _activationTime = time;
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
        _feederFixIdentifier = feederFixIdentifier;
        return this;
    }

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate, bool manual = false)
    {
        _feederFixEstimate = estimate;
        _manualFeederFixEstimate = manual;
        return this;
    }

    public FlightBuilder WithFeederFixEstimate(DateTimeOffset estimate, TimeSpan arrivalInterval, bool manual = false)
    {
        _feederFixEstimate = estimate;
        _manualFeederFixEstimate = manual;
        _landingEstimate = estimate + arrivalInterval;
        return this;
    }

    public FlightBuilder WithFeederFixTime(DateTimeOffset time)
    {
        _feederFixTime = time;
        return this;
    }

    public FlightBuilder PassedFeederFixAt(DateTimeOffset time)
    {
        _passedFeederFix = time;
        return this;
    }

    public FlightBuilder WithLandingEstimate(DateTimeOffset estimate)
    {
        _landingEstimate = estimate;
        return this;
    }

    public FlightBuilder WithTargetLandingTime(DateTimeOffset targetTime)
    {
        _targetLandingTime = targetTime;
        return this;
    }

    public FlightBuilder WithLandingTime(DateTimeOffset time)
    {
        _landingTime = time;
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

    public FlightBuilder WithTrajectory(TimeSpan timeToGo)
    {
        _timeToGo = timeToGo;
        return this;
    }

    public FlightBuilder AsManuallyInserted(bool value = true)
    {
        _isManuallyInserted = value;
        return this;
    }

    public Flight Build()
    {
        var trajectory = new Trajectory(_timeToGo);

        Flight flight;
        if (_isManuallyInserted)
        {
            // Use the dummy flight constructor
            flight = new Flight(
                callsign: callsign,
                aircraftType: _aircraftType,
                aircraftCategory: _aircraftCategory,
                wakeCategory: _wakeCategory,
                destinationIdentifier: _destination,
                assignedRunwayIdentifier: _assignedRunway,
                approachType: _approachType,
                trajectory: trajectory,
                targetLandingTime: _targetLandingTime ?? _landingTime,
                state: _state);
        }
        else
        {
            // Use the real flight constructor
            flight = new Flight(
                callsign: callsign,
                aircraftType: _aircraftType,
                aircraftCategory: _aircraftCategory,
                wakeCategory: _wakeCategory,
                destinationIdentifier: _destination,
                originIdentifier: _origin,
                isFromDepartureAirport: _isFromDepartureAirport,
                estimatedDepartureTime: null,
                assignedRunwayIdentifier: _assignedRunway,
                approachType: _approachType,
                trajectory: trajectory,
                feederFixIdentifier: _feederFixIdentifier,
                feederFixEstimate: _feederFixEstimate != default ? _feederFixEstimate : null,
                landingEstimate: _landingEstimate,
                activatedTime: _activationTime,
                fixes: [
                    new FixEstimate(_feederFixIdentifier ?? _destination, _feederFixEstimate != default ? _feederFixEstimate : _landingEstimate.Subtract(_timeToGo), _passedFeederFix),
                    new FixEstimate(_destination, _landingEstimate)
                ],
                position: _position);
        }

        // Update state
        flight.SetState(_state, new FixedClock(_activationTime));

        // Update feeder fix estimate if specified
        if (_feederFixEstimate != default && !string.IsNullOrEmpty(_feederFixIdentifier))
        {
            flight.UpdateFeederFixEstimate(_feederFixEstimate, _manualFeederFixEstimate);
        }

        // Mark as passed feeder fix
        if (_passedFeederFix.HasValue && !flight.HasPassedFeederFix)
        {
            flight.PassedFeederFix(_passedFeederFix.Value);
        }

        // Set sequence data
        var landingTimeToUse = _landingTime != default ? _landingTime : flight.LandingEstimate;
        flight.SetSequenceData(landingTimeToUse, FlowControls.ProfileSpeed);

        flight.UpdateLastSeen(new FixedClock(_lastSeen));

        flight.SetMaximumDelay(_manualDelay);
        flight.HighPriority = _highPriority;

        if (_targetLandingTime.HasValue && !_isManuallyInserted)
        {
            flight.SetTargetLandingTime(_targetLandingTime.Value);
        }

        return flight;
    }
}
