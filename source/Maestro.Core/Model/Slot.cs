namespace Maestro.Core.Model;

public class Slot
{
    public Slot(Guid id, DateTimeOffset start, DateTimeOffset end, string[] runwayIdentifiers)
    {
        if (start >= end)
            throw new MaestroException("Start time must be before end time.");

        Id = id;
        StartTime = start;
        EndTime = end;
        RunwayIdentifiers = runwayIdentifiers;
    }

    public Guid Id { get; }
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
    public string[] RunwayIdentifiers { get; }
}
