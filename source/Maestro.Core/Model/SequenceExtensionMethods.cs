using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(
        this Sequence sequence,
        Flight flight,
        bool displaceStableFlights = false)
    {
        // TEMP: Trialing removing this logic, as unstable flights in front of inserted (frozen) flights would end up
        // getting delayed all the way behind them.
        // Let the scheduling move them naturally instead.
        // TODO: We'll need to revisit this once the Scheduler has been refactored

        // var earliestIndex = 0;
        // if (!displaceStableFlights)
        // {
        //     earliestIndex = sequence.FindLastIndex(f =>
        //         f.State is not State.Unstable and not State.Stable &&
        //         f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier) + 1;
        // }

        var newIndex = sequence.FindIndex(
            // Math.Max(earliestIndex, 0),
            f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
                 f.LandingEstimate.IsAfter(flight.LandingEstimate));

        if (newIndex == -1)
            newIndex = sequence.Flights.Count;

        sequence.Move(flight, newIndex, displaceStableFlights);
    }
}
