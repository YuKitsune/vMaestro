using Maestro.Contracts.Flights;
using Maestro.Contracts.Shared;

namespace Maestro.Wpf.ViewModels;

public class FlightViewModel
{
    public FlightViewModel()
    {
        Callsign = "QFA1234";
        AircraftType = "B744";
        WakeCategory = Maestro.Contracts.Shared.WakeCategory.Heavy;
        OriginIdentifier = "YMML";
        DestinationIdentifier = "YSSY";
        State = State.Unstable;
        NumberInSequence = 3;
        FeederFixIdentifier = "RIVET";
        InitialFeederFixEstimate = DateTimeOffset.Now.AddMinutes(1);
        FeederFixEstimate = DateTimeOffset.Now.AddMinutes(2);
        FeederFixTime = DateTimeOffset.Now.AddMinutes(3);
        AssignedRunway = "34L";
        NumberToLandOnRunway = 5;
        InitialLandingEstimate = DateTimeOffset.Now.AddMinutes(5);
        LandingEstimate = DateTimeOffset.Now.AddMinutes(6);
        LandingTime = DateTimeOffset.Now.AddMinutes(7);
        RequiredControlAction = ControlAction.PathStretching;
        RemainingControlAction = ControlAction.Resume;
        HighSpeed = false;
    }

    public FlightViewModel(FlightDto flight)
    {
        Callsign = flight.Callsign;
        AircraftType = flight.AircraftType;
        WakeCategory = flight.WakeCategory;
        OriginIdentifier = flight.OriginIdentifier;
        DestinationIdentifier = flight.DestinationIdentifier;
        IsFromDepartureAirport = flight.IsFromDepartureAirport;
        State = flight.State;
        NumberInSequence = flight.NumberInSequence;
        FeederFixIdentifier = flight.FeederFixIdentifier;
        InitialFeederFixEstimate = flight.InitialFeederFixEstimate;
        FeederFixEstimate = flight.FeederFixEstimate;
        FeederFixTime = flight.FeederFixTime;
        AssignedRunway = flight.AssignedRunwayIdentifier;
        NumberToLandOnRunway = flight.NumberToLandOnRunway;
        InitialLandingEstimate = flight.InitialLandingEstimate;
        LandingEstimate = flight.LandingEstimate;
        LandingTime = flight.LandingTime;
        RequiredControlAction = flight.RequiredControlAction;
        RemainingControlAction = flight.RemainingControlAction;
        HighSpeed = flight.HighSpeed;
    }
    public string Callsign { get; }
    public string? AircraftType { get; }
    public WakeCategory? WakeCategory { get; }
    public string? OriginIdentifier { get; }
    public string DestinationIdentifier { get; }
    public bool IsFromDepartureAirport { get; }
    public State State { get; }
    public int NumberInSequence { get; }
    public string? FeederFixIdentifier { get; }
    public DateTimeOffset? InitialFeederFixEstimate { get; }
    public DateTimeOffset? FeederFixEstimate { get; }
    public DateTimeOffset? FeederFixTime { get; }
    public string AssignedRunway { get; }
    public int NumberToLandOnRunway { get; }
    public DateTimeOffset InitialLandingEstimate { get; }
    public DateTimeOffset LandingEstimate { get; }
    public DateTimeOffset LandingTime { get; }
    public ControlAction RequiredControlAction { get; }
    public ControlAction RemainingControlAction { get; }

    public bool HighSpeed { get; }
}
