// using Maestro.Core.Configuration;
// using Maestro.Core.Extensions;
// using Maestro.Core.Handlers;
//
// namespace Maestro.Core.Model;
//
// public class ChangeRunwayModeRequestHandler2
// {
//     readonly ISlotBasedScheduler _scheduler;
//
//     public async Task ChangeRunwayMode(ChangeRunwayModeRequest changeRunwayModeRequest, CancellationToken cancellationToken)
//     {
//         var sequence = new SlotBasedSequence();
//
//         // 1. Get a list of affected flights
//         var existingSlots = sequence.Slots
//             .Where(s => s.Time >= changeRunwayModeRequest.StartTime)
//             .ToList();
//
//         var affectedFlights = existingSlots
//             .Select(s => s.Flight)
//             .WhereNotNull()
//             .ToList();
//
//         // 2. Reprovision the new slots based on the new runway mode
//         sequence.ReprovisionSlotsFrom(changeRunwayModeRequest.StartTime, _scheduler);
//
//         // 3. Allocate each affected flight a new slot based on the new runway mode (effectively re-sequencing them)
//         foreach (var flight in affectedFlights)
//         {
//             flight.NeedsRecompute = true;
//             _scheduler.AllocateSlot(sequence, flight);
//         }
//
//         // 4. Change the runway mode in the sequence
//         sequence.ChangeRunwayMode(
//             new RunwayMode(changeRunwayModeRequest.RunwayMode),
//             changeRunwayModeRequest.StartTime);
//     }
// }
