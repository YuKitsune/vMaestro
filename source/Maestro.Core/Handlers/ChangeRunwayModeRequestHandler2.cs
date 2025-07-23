using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;

namespace Maestro.Core.Model;

public class ChangeRunwayModeRequestHandler2
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly IRunwayAssigner _runwayAssigner;
    readonly ISlotBasedScheduler _scheduler;

    public async Task ChangeRunwayMode(ChangeRunwayModeRequest changeRunwayModeRequest, CancellationToken cancellationToken)
    {
        var sequence = new SlotBasedSequence();
        var airportConfiguration = _airportConfigurationProvider
            .GetAirportConfigurations()
            .Single(a => a.Identifier == changeRunwayModeRequest.AirportIdentifier);

        // 1. Get a list of affected flights
        var existingSlots = sequence.Slots
            .Where(s => s.Time >= changeRunwayModeRequest.StartTime)
            .ToList();

        var affectedFlights = existingSlots
            .Select(s => s.Flight)
            .WhereNotNull()
            .ToList();

        // 2. Reprovision the new slots based on the new runway mode
        sequence.ReprovisionSlotsFrom(changeRunwayModeRequest.StartTime, _scheduler);

        foreach (var flight in affectedFlights)
        {
            // 2. For each flight, change the runway based on the new runway mode
            var newRunway = FindBestRunway(
                _runwayAssigner,
                flight.FeederFixIdentifier,
                flight.AircraftType,
                new RunwayMode(changeRunwayModeRequest.RunwayMode),
                airportConfiguration.RunwayAssignmentRules);

            flight.SetRunway(newRunway, manual: false);
            flight.NeedsRecompute = true;

            // 4. Allocate each affected flight a new slot based on the new runway mode (effectively re-sequencing them)
            _scheduler.Schedule(sequence, flight);
        }

        // 5. Change the runway mode in the sequence
        sequence.ChangeRunwayMode(
            new RunwayMode(changeRunwayModeRequest.RunwayMode),
            changeRunwayModeRequest.StartTime);
    }
}
