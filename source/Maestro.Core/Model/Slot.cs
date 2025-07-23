namespace Maestro.Core.Model;

public class Slot(string runwayIdentifier, DateTimeOffset time, TimeSpan duration)
{
    public string RunwayIdentifier { get; } = runwayIdentifier;
    public DateTimeOffset Time { get; } = time;
    public TimeSpan Duration { get; } = duration;
    public Flight? Flight { get; private set; }
    public bool Reserved { get; init; }
    public bool IsAvailable => Flight is null && !Reserved;

    public void AllocateTo(Flight flight, bool manual = false)
    {
        if (Flight is not null)
            throw new MaestroException("Slot is already allocated to a flight");

        if (Reserved)
            throw new MaestroException("Slot is reserved and cannot be allocated to a flight");

        ApplyTimes(flight, manual);
        flight.SetRunway(RunwayIdentifier, manual);
        Flight = flight;
    }

    public void Deallocate()
    {
        if (Flight == null)
            throw new MaestroException("Slot is not allocated to a flight");

        Flight = null;
    }

    void ApplyTimes(Flight flight, bool manual)
    {
        if (flight.EstimatedFeederFixTime is not null && !flight.HasPassedFeederFix)
        {
            var totalDelay = Time - flight.EstimatedLandingTime;
            var feederFixTime = flight.EstimatedFeederFixTime.Value + totalDelay;
            flight.SetFeederFixTime(feederFixTime);
        }

        flight.SetLandingTime(Time, manual);
    }

    public override string ToString()
    {
        return Time.ToString("dd/HHmm");
    }
}
