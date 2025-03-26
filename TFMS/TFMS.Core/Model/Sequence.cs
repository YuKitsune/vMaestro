using MediatR;
using TFMS.Core.DTOs;

namespace TFMS.Core.Model;

public class Sequence(IMediator mediator, string airportIdentifier)
{
    readonly IMediator mediator = mediator;
    readonly List<Arrival> arrivals = new();

    public string AirportIdentifier { get; } = airportIdentifier;

    public IReadOnlyList<Arrival> Arrivals => arrivals;

    public void Add(string callsign, string origin, string destination, string? assignedRunway, AircraftPerformanceData performanceData, DateTimeOffset initialFeederFixEstimate, DateTimeOffset initialDestinationEstimate)
    {
        if (arrivals.Any(c => c.Callsign == callsign))
        {
            throw new Exception($"{callsign} is already in the sequence for {AirportIdentifier}");
        }

        if (destination != AirportIdentifier)
        {
            throw new Exception($"Cannot add {callsign} to sequence for {AirportIdentifier} as the destination is {destination}");
        }

        arrivals.Add(new Arrival(callsign, origin, destination, assignedRunway, performanceData, initialFeederFixEstimate, initialDestinationEstimate));

        mediator.Publish(new SequenceModifiedNotification(this));
    }
}
