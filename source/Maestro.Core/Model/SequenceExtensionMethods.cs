using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(
        this Sequence sequence,
        Flight flight)
    {
        var newIndex = sequence.FindIndex(
            f => f.FeederFixEstimate.IsAfter(flight.FeederFixEstimate));

        if (newIndex == -1)
            newIndex = sequence.Flights.Count;

        sequence.Move(flight, newIndex);
    }
}
