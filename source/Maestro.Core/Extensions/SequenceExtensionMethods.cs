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
            Flights = sequence.Flights.Select(f => f.ToMessage(sequence))
                .ToArray(),
            CurrentRunwayMode = sequence.CurrentRunwayMode.ToMessage(),
            NextRunwayMode = sequence.NextRunwayMode?.ToMessage(),
            LastLandingTimeForCurrentMode = sequence.LastLandingTimeForCurrentMode,
            FirstLandingTimeForNextMode = sequence.FirstLandingTimeForNextMode,
            Slots = sequence.Slots.Select(s => s.ToMessage()).ToArray()
        };
    }
}
