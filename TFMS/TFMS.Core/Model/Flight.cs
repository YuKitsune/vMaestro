namespace TFMS.Core.Model;

public class Flight(string callsign, string aircraftType, string origin, string destination, string feederFix, string? runway, string? star, DateTimeOffset initialFeederFixEstimate, DateTimeOffset initialDestinationEstimate)
{
    public string Callsign { get; } = callsign;
    public string AircraftType { get; } = aircraftType;
    public string OriginIcaoCode { get; } = origin;
    public string DestinationIcaoCode { get; } = destination;

    public State State { get; } = State.Unstable;

    public string FeederFix { get; private set; } = feederFix;
    public string? AssignedRunway { get; private set; } = runway;
    public string? AssignedStar { get; private set; } = star;

    // TODO: What can change the initial estimate?
    public DateTimeOffset InitialFeederFixTime { get; } = initialFeederFixEstimate;
    public DateTimeOffset EstimatedFeederFixTime { get; private set; } = initialFeederFixEstimate;
    public DateTimeOffset ScheduledFeederFixTime { get; private set; } = DateTimeOffset.MinValue; // TODO

    // TODO: What can change the initial estimate?
    public DateTimeOffset InitialLandingTime { get; } = initialDestinationEstimate;
    public DateTimeOffset EstimatedLandingTime { get; private set; } = initialDestinationEstimate;
    public DateTimeOffset ScheduledLandingTime { get; private set; } = DateTimeOffset.MinValue; // TODO

    /// <summary>
    ///     Difference between the flight’s initial ETA and its scheduled time over the landing threshold.
    /// </summary>
    public TimeSpan TotalDelay => InitialLandingTime - ScheduledLandingTime;

    /// <summary>
    ///     The amount of delaying action still required to reach the landing threshold at the scheduled time.
    /// </summary>
    public TimeSpan RemainingDelay => EstimatedLandingTime - ScheduledLandingTime;

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