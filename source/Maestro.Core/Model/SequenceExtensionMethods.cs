using Maestro.Core.Extensions;

namespace Maestro.Core.Model;

public static class SequenceExtensionMethods
{
    public static void RepositionByEstimate(
        this Sequence sequence,
        Flight flight,
        bool displaceStableFlights = false)
    {
        var newIndex = sequence.FindIndex(
            f => f.AssignedRunwayIdentifier == flight.AssignedRunwayIdentifier &&
                 f.LandingEstimate.IsAfter(flight.LandingEstimate));

        if (newIndex == -1)
            newIndex = sequence.Flights.Count;

        sequence.Move(flight, newIndex, displaceStableFlights);
    }
}
