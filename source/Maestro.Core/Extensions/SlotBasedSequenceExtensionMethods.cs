using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Extensions;

public static class SlotBasedSequenceExtensionMethods
{
    public static SlotBasedSequenceDto ToDto(this SlotBasedSequence sequence)
    {
        return new SlotBasedSequenceDto
        {
            AirportIdentifier = sequence.AirportIdentifier,
            CurrentRunwayMode = sequence.CurrentRunwayMode.ToMessage(),
            NextRunwayMode = sequence.NextRunwayMode?.ToMessage(),
            RunwayModeChangeTime = sequence.RunwayModeChangeTime,
            PendingFlights = sequence.PendingFlights.Select(f => f.Callsign).ToArray(),
            DesequencedFlights = sequence.DesequencedFlights.Select(f => f.Callsign).ToArray(),
            LandedFlights = sequence.LandedFlights.Select(f => f.Callsign).ToArray(),
            Slots = sequence.Slots.Select(s =>
                    new SlotDto
                    {
                        Identifier = s.Identifier,
                        RunwayIdentifier = s.RunwayIdentifier,
                        Time = s.Time,
                        Flight = s.Flight?.ToMessage(sequence),
                        Reserved = s.Reserved
                    })
                .ToArray()
        };
    }
}
