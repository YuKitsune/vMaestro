namespace TFMS.Core.Model;

public enum SequenceState
{
    Unstable,
    Stable,
    SuperStable,
    Frozen,
    Landed
}

public enum WakeCategory
{
    Light,
    Medium,
    Heavy,
    Super
}

public record AircraftPerformanceData(
    string TypeCode,
    WakeCategory WakeCategory);

public class Arrival(string callsign, string origin, string destination, string feederFix, string? runway, AircraftPerformanceData performanceData, DateTimeOffset initialFeederFixEstimate, DateTimeOffset initialDestinationEstimate)
{
    public string Callsign { get; } = callsign;
    public string OriginIcaoCode { get; } = origin;
    public string DestinationIcaoCode { get; } = destination;
    public AircraftPerformanceData PerformanceData { get; } = performanceData;

    public SequenceState State { get; } = SequenceState.Unstable;

    public string FeederFix { get; private set; } = feederFix;
    public string? AssignedRunway { get; private set; } = runway;

    // TODO: What can change the initial estimate?
    public DateTimeOffset InitialFeederFixTime { get; } = initialFeederFixEstimate;
    public DateTimeOffset EstimatedFeederFixTime { get; private set; } = initialFeederFixEstimate;
    public DateTimeOffset? ScheduledFeederFixTime { get; private set; }

    // TODO: What can change the initial estimate?
    public DateTimeOffset InitialLandingTime { get; } = initialDestinationEstimate;
    public DateTimeOffset EstimatedLandingTime { get; private set; } = initialDestinationEstimate;
    public DateTimeOffset? ScheduledLandingTime { get; private set; }

    /// <summary>
    ///     Difference between the flight’s initial ETA and its scheduled time over the landing threshold.
    /// </summary>
    public TimeSpan? TotalDelay => InitialLandingTime - ScheduledLandingTime;

    /// <summary>
    ///     The amount of delaying action still required to reach the landing threshold at the scheduled time.
    /// </summary>
    public TimeSpan? RemainingDelay => EstimatedLandingTime - ScheduledLandingTime;

    public void AssignRunway(string runway)
    {
        AssignedRunway = runway;
    }

    public void UpdateFeederFixEstimate(DateTimeOffset feederFixEstimate)
    {
        EstimatedFeederFixTime = feederFixEstimate;
    }

    public void UpdateLanidngEstimate(DateTimeOffset landingEstimate)
    {
        EstimatedLandingTime = landingEstimate;
    }
}