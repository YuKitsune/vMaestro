using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class SequenceExtensionMethods
{
    public static SequenceMessage ToMessage(this Sequence sequence)
    {
        return new SequenceMessage
        {
            AirportIdentifier = sequence.AirportIdentifier,
            Flights = sequence.Flights.Where(f => f.IsInSequence)
                .Select(f => f.ToMessage(sequence))
                .ToArray(),
            DesequencedFlights = sequence.Flights.Where(f => f.State is State.Desequenced)
                .Select(f => f.Callsign)
                .ToArray(),
            LandedFlights = sequence.Flights.Where(f => f.State == State.Landed)
                .Select(f => f.Callsign)
                .ToArray(),
            PendingFlights = sequence.PendingFlights.Select(f => f.Callsign).ToArray(),
            CurrentRunwayMode = sequence.CurrentRunwayMode.ToMessage(),
            NextRunwayMode = sequence.NextRunwayMode?.ToMessage(),
            LastLandingTimeForCurrentMode = sequence.LastLandingTimeForCurrentMode,
            FirstLandingTimeForNextMode = sequence.FirstLandingTimeForNextMode,
            Slots = sequence.Slots.Select(s => s.ToMessage()).ToArray()
        };
    }
}
