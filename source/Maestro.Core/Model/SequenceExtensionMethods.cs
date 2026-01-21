using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(
        this Sequence sequence,
        Flight flight)
    {
        var newIndex = sequence.FindIndex(
            f => f.LandingEstimate.IsAfter(flight.LandingEstimate));

        if (newIndex == -1)
            newIndex = sequence.Flights.Count;

        sequence.Move(flight, newIndex);
    }

    public static void InsertByTargetTime(
        this Sequence sequence,
        Flight flight,
        DateTimeOffset targetLandingTime)
    {
        // New flights can be inserted ahead of Stable ones
        // Find the index of the first flight on the same runway that isn't Unstable or Stable
        // New flights cannot be inserted beyond this point
        var earliestInsertionIndex = sequence.FindLastIndex(f =>
            f.State is not State.Unstable and not State.Stable &&
            f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier) + 1;

        // Determine the insertion point by landing estimate
        // TODO: Refactor this to use the feeder fix time if available
        var insertionIndex = sequence.FindIndex(
            earliestInsertionIndex,
            f => f.LandingEstimate.IsAfter(targetLandingTime));

        if (insertionIndex == -1)
            insertionIndex = Math.Min(earliestInsertionIndex, sequence.Flights.Count);

        sequence.Insert(insertionIndex, flight);
    }
}
