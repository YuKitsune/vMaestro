using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Wpf.ViewModels;

public class FlightViewModel
{
    public FlightViewModel()
    {
        Callsign = "QFA1234";
        AircraftType = "B744";
        WakeCategory = Core.Model.WakeCategory.Heavy;
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
        InitialDelay = TimeSpan.FromMinutes(2);
        RemainingDelay = TimeSpan.FromMinutes(4);
        FlowControls = FlowControls.ReduceSpeed;
    }

    public FlightViewModel(FlightMessage flight)
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
        AssignedRunway = flight.AssignedRunway;
        NumberToLandOnRunway = flight.NumberToLandOnRunway;
        InitialLandingEstimate = flight.InitialLandingEstimate;
        LandingEstimate = flight.LandingEstimate;
        LandingTime = flight.LandingTime;
        InitialDelay = flight.InitialDelay;
        RemainingDelay = flight.RemainingDelay;
        FlowControls = flight.FlowControls;
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
    public string? AssignedRunway { get; }
    public int NumberToLandOnRunway { get; }
    public DateTimeOffset InitialLandingEstimate { get; }
    public DateTimeOffset LandingEstimate { get; }
    public DateTimeOffset LandingTime { get; }
    public TimeSpan InitialDelay { get; }
    public TimeSpan RemainingDelay { get; }
    public FlowControls FlowControls { get; }
}
