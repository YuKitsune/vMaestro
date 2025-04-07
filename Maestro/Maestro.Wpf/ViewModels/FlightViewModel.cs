using Maestro.Core.Model;

namespace Maestro.Wpf.ViewModels;

public class FlightViewModel
{
    public FlightViewModel(string callsign, string aircraftType, WakeCategory wakeCategory, string origin, string destination, int numberToLand, string feederFixIdentifier, DateTimeOffset? initialFeederFixTime, DateTimeOffset? currentFeederFixTime, DateTimeOffset? scheduledFeederFixTime, string assignedRunway, int numberToLandOnRunway, DateTimeOffset initialLandingTime, DateTimeOffset currentLandingTime, DateTimeOffset scheduledLandingTime, TimeSpan initialDelay, TimeSpan currentDelay)
    {
        Callsign = callsign;
        AircraftType = aircraftType;
        WakeCategory = wakeCategory;
        Origin = origin;
        Destination = destination;
        NumberToLand = numberToLand;
        FeederFixIdentifier = feederFixIdentifier;
        InitialFeederFixTime = initialFeederFixTime;
        CurrentFeederFixTime = currentFeederFixTime;
        ScheduledFeederFixTime = scheduledFeederFixTime;
        AssignedRunway = assignedRunway;
        NumberToLandOnRunway = numberToLandOnRunway;
        InitialLandingTime = initialLandingTime;
        CurrentLandingTime = currentLandingTime;
        ScheduledLandingTime = scheduledLandingTime;
        InitialDelay = initialDelay;
        CurrentDelay = currentDelay;
    }

    public string Callsign { get; }
    public string AircraftType { get; }
    public WakeCategory WakeCategory { get; }
    public string Origin { get; }
    public string Destination { get; }

    public int NumberToLand { get; }
    
    public string FeederFixIdentifier { get; }
    public DateTimeOffset? InitialFeederFixTime { get; }
    public DateTimeOffset? CurrentFeederFixTime { get; }
    public DateTimeOffset? ScheduledFeederFixTime { get; }
    
    public string? AssignedRunway { get; }
    public int NumberToLandOnRunway { get; }
    
    public DateTimeOffset InitialLandingTime { get; }
    public DateTimeOffset CurrentLandingTime { get; }
    public DateTimeOffset ScheduledLandingTime { get; }
    
    public TimeSpan InitialDelay { get; }
    public TimeSpan CurrentDelay { get; }
}