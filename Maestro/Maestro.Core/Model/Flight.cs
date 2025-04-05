
namespace Maestro.Core.Model;

public class Flight
{
    public required string Callsign { get; init; }
    public required string AircraftType { get; init; }
    public required string OriginIdentifier { get; init; }
    public required string DestinationIdentifier { get; init; }
    public required string? FeederFixIdentifier { get; set; }
    public string? AssignedRunwayIdentifier { get; set; }
    public string? AssignedStarIdentifier { get; set; }
    public required bool HighPriority { get; init; }
    public bool NoDelay { get; set; }

    public State State { get; } = State.Unstable;

    public DateTimeOffset? InitialFeederFixTime { get; private set; }
    public DateTimeOffset? EstimatedFeederFixTime { get; private set; } // ETA_FF
    public DateTimeOffset? ScheduledFeederFixTime { get; private set; } // STA_FF
    public TimeSpan? TotalDelayToFeederFix => InitialFeederFixTime - ScheduledFeederFixTime;
    public TimeSpan? RemainingDelayToFeederFix => EstimatedFeederFixTime - ScheduledFeederFixTime;

    public DateTimeOffset InitialLandingTime { get; private set; }
    public DateTimeOffset EstimatedLandingTime { get; private set; } // ETA
    public DateTimeOffset ScheduledLandingTime { get; private set; } // STA
    public TimeSpan TotalDelayToRunway => InitialLandingTime - ScheduledLandingTime;
    public TimeSpan RemainingDelayToRunway => EstimatedLandingTime - ScheduledLandingTime;
    
    // TODO
    public DateTimeOffset Activated { get; private set; }

    public void SetFeederFix(string feederFixIdentifier, DateTimeOffset feederFixEstimate)
    {
        FeederFixIdentifier = feederFixIdentifier;
        UpdateFeederFixEstimate(feederFixEstimate);
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate)
    {
        EstimatedFeederFixTime = feederFixEstimate;
        if (State == State.Unstable)
        {
            InitialFeederFixTime = feederFixEstimate;
        }
    }

    public void UpdateLandingEstimate(DateTimeOffset landingEstimate)
    {
        EstimatedLandingTime = landingEstimate;
        if (State == State.Unstable)
        {
            InitialLandingTime = landingEstimate;
        }
    }
}