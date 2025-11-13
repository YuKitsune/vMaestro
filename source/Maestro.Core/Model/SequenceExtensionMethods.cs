using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(this Sequence sequence, Flight flight, bool displaceStableFlights = false)
    {
        var earliestIndex = 0;
        if (!displaceStableFlights)
        {
            earliestIndex = sequence.FindLastIndex(f =>
                f.State is not State.Unstable and not State.Stable &&
                f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier);
        }

        var newIndex = sequence.FindIndex(
            Math.Max(earliestIndex, 0),
            f => f.LandingEstimate.IsBefore(flight.LandingEstimate)) + 1;

        sequence.Move(flight, newIndex, displaceStableFlights);
    }
}
