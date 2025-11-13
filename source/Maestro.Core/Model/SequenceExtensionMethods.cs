using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(this Sequence sequence, Flight flight, bool displaceStableFlights = false)
    {
        var earliestIndex = -1;
        if (!displaceStableFlights)
        {
            earliestIndex = sequence.LastIndexOf(f => f.State is not State.Unstable and not State.Stable) + 1;
        }

        var desiredIndex = sequence.FirstIndexOf(f => f.LandingEstimate.IsBefore(flight.LandingEstimate));

        var newIndex = Math.Max(earliestIndex,  desiredIndex);

        sequence.Move(flight, newIndex, displaceStableFlights);
    }
}
