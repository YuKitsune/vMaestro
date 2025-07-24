using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Serilog;

namespace Maestro.Core.Model;

// TODO Test cases:
// - Reprovisioning slots from a specific time
//   - Cannot provision slots too far in the future
//   - Deletes existing slots after the specified time
//   - Retains existing slots before the specified time
//   - Does not create gaps in the sequence
//   - Reschedules affected flights (use NSubstitute to record the call count)
// - Provisioning slots from a specific time
//   - Cannot provision slots too far in the future
//   - Does not delete existing slots
//   - Does not create overlapping slots
//   - Accounts for runway mode changes in the future
// - Changing runway mode
//   - Changes the current runway mode immediately
//   - Schedules a runway mode change for a future time
//   - Reprovisions slots based on the new runway mode
//   - Returns a list of flights that need to be rescheduled

public class SlotBasedSequence
{
    static TimeSpan _maxProvisionTime = TimeSpan.FromHours(2);

    readonly AirportConfiguration _airportConfiguration;

    readonly List<Slot> _slots = [];

    public string AirportIdentifier => _airportConfiguration.Identifier;

    public IReadOnlyList<Slot> Slots => _slots;

    public RunwayMode CurrentRunwayMode { get; private set; }
    public RunwayMode? NextRunwayMode { get; private set; }
    public DateTimeOffset RunwayModeChangeTime { get; private set; }

    public SlotBasedSequence(AirportConfiguration airportConfiguration, RunwayMode runwayMode, DateTimeOffset startTime)
    {
        _airportConfiguration = airportConfiguration;
        CurrentRunwayMode = runwayMode;
        ProvisionSlotsFrom(startTime);
    }

    public void ReprovisionSlotsFrom(DateTimeOffset startTime, ISlotBasedScheduler scheduler)
    {
        if (startTime > DateTime.MaxValue.Add(_maxProvisionTime.Negate()))
            throw new MaestroException($"Cannot provision slots for {startTime} as it is too far in the future.");

        var affectedFlights = _slots
            .Where(s => s.Time >= startTime)
            .Select(s => s.Flight)
            .WhereNotNull()
            .ToList();

        var slotsToRemove = _slots.Where(s => s.Time >= startTime).ToArray();
        foreach (var slot in slotsToRemove)
        {
            _slots.Remove(slot);
        }

        ProvisionSlotsFrom(startTime);

        foreach (var affectedFlight in affectedFlights)
        {
            scheduler.AllocateSlot(this, affectedFlight);
        }
    }

    public RunwayMode RunwayModeAt(DateTimeOffset time)
    {
        return NextRunwayMode is not null && RunwayModeChangeTime.IsSameOrBefore(time)
            ? NextRunwayMode
            : CurrentRunwayMode;
    }

    public void ProvisionSlotsFrom(DateTimeOffset startTime)
    {
        if (startTime > DateTime.MaxValue.Add(_maxProvisionTime.Negate()))
            throw new MaestroException($"Cannot provision slots for {startTime} as it is too far in the future.");

        var endTime = startTime + _maxProvisionTime;

        if (NextRunwayMode is not null)
        {
            foreach (var runwayConfiguration in NextRunwayMode.Runways)
            {
                ProvisionSlots(RunwayModeChangeTime, endTime, runwayConfiguration);
            }

            endTime = RunwayModeChangeTime;
        }

        foreach (var runwayConfiguration in CurrentRunwayMode.Runways)
        {
            ProvisionSlots(startTime, endTime, runwayConfiguration);
        }
    }

    void ProvisionSlots(DateTimeOffset startTime, DateTimeOffset endTime, RunwayConfiguration runway)
    {
        var currentTime = startTime;
        while (currentTime < endTime)
        {
            var duration = TimeSpan.FromSeconds(runway.LandingRateSeconds);

            // Ensure no overlapping slots
            if (!_slots.Any(s =>
                    s.RunwayIdentifier == runway.Identifier &&
                    s.Time < currentTime + duration &&
                    s.Time + s.Duration > currentTime))
            {
                _slots.Add(new Slot(runway.Identifier, currentTime, duration));
            }

            currentTime += duration;
        }
    }

    public void RecordGaps(ILogger logger)
    {
        if (_slots.Count < 2)
            return;

        // Sort the slots by their start time
        var orderedSlots = _slots.OrderBy(s => s.Time).ToList();

        for (int i = 0; i < orderedSlots.Count - 1; i++)
        {
            var currentSlot = orderedSlots[i];
            var nextSlot = orderedSlots[i + 1];

            if (currentSlot.Time + currentSlot.Duration < nextSlot.Time)
            {
                var gapStart = currentSlot.Time + currentSlot.Duration;
                var gapEnd = nextSlot.Time;
                logger.Information($"Gap detected between slots: {gapStart} to {gapEnd}");
            }
        }
    }

    /// <summary>
    ///     Changes the runway mode with an immediate effect.
    /// </summary>
    public void ChangeRunwayMode(RunwayMode runwayMode, ISlotBasedScheduler scheduler, IClock clock)
    {
        CurrentRunwayMode = runwayMode;
        NextRunwayMode = null;
        RunwayModeChangeTime = default;

        ReprovisionSlotsFrom(clock.UtcNow(), scheduler);
    }

    /// <summary>
    ///     Schedules a runway mode change for some time in the future.
    /// </summary>
    public void ChangeRunwayMode(
        RunwayMode runwayMode,
        DateTimeOffset changeTime,
        ISlotBasedScheduler scheduler)
    {
        NextRunwayMode = runwayMode;
        RunwayModeChangeTime = changeTime;

        ReprovisionSlotsFrom(changeTime, scheduler);
    }
}
