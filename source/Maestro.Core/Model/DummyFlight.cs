using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public class DummyFlight : IEquatable<DummyFlight>, IComparable<DummyFlight>
{
    public DummyFlight(string callsign, string? aircraftType, string runwayIdentifier, DateTimeOffset landingTime, State state)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        AssignedRunwayIdentifier = runwayIdentifier;
        LandingTime = landingTime;
        State = state;
    }

    public DummyFlight(DummyFlightMessage message)
    {
        Callsign = message.Callsign;
        AircraftType = message.AircraftType;
        AssignedRunwayIdentifier = message.AssignedRunwayIdentifier;
        LandingTime = message.LandingTime;
        State = message.State;
    }

    public string Callsign { get; }
    public string? AircraftType { get; }
    public string AssignedRunwayIdentifier { get; private set; }
    public DateTimeOffset LandingTime { get; private set; }
    public State State { get; private set; }

    public void SetState(State newState)
    {
        State = newState;
    }

    public void SetRunway(string runwayIdentifier)
    {
        AssignedRunwayIdentifier = runwayIdentifier;
    }

    public void SetLandingTime(DateTimeOffset landingTime)
    {
        LandingTime = landingTime;
    }

    public bool Equals(DummyFlight? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Callsign == other.Callsign;
    }

    public override bool Equals(object? obj)
    {
        return obj is DummyFlight other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Callsign.GetHashCode();
    }

    public int CompareTo(DummyFlight? other)
    {
        if (other is null) return 1;
        return LandingTime.CompareTo(other.LandingTime);
    }

    public static bool operator ==(DummyFlight? left, DummyFlight? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DummyFlight? left, DummyFlight? right)
    {
        return !Equals(left, right);
    }

    public DummyFlightMessage ToMessage()
    {
        return new DummyFlightMessage
        {
            Callsign = Callsign,
            AircraftType = AircraftType,
            AssignedRunwayIdentifier = AssignedRunwayIdentifier,
            LandingTime = LandingTime,
            State = State
        };
    }
}
