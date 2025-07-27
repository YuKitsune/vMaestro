using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Handlers;

public class SequenceCleaner(IClock clock, ILogger logger)
{
    // TODO: Make configurable
    readonly TimeSpan _slotPurgeTime = TimeSpan.FromMinutes(5);
    readonly TimeSpan _lostFlightTimeout = TimeSpan.FromHours(1);

    public void CleanUpFlights(SlotBasedSequence sequence)
    {
        var now = clock.UtcNow();
        var purgeCutoffTime = now.Subtract(_slotPurgeTime);
        sequence.PurgeEmptySlotsBefore(purgeCutoffTime);
        sequence.PurgeLandedFlights();

        var slotsWithLostFlights = sequence.Slots
            .Where(s => s.Flight is not null)
            .Where(s => now - s.Flight!.LastSeen >= _lostFlightTimeout)
            .ToArray();

        foreach (var slot in slotsWithLostFlights)
        {
            logger.Information(
                "Removing {Callsign} from {AirportIdentifier} as it has not been seen for {Duration}.",
                slot.Flight!.Callsign,
                sequence.AirportIdentifier,
                _lostFlightTimeout.ToHoursAndMinutesString());
            slot.Deallocate();
        }
    }
}
