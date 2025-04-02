using MediatR;
using TFMS.Core.Dtos.Messages;

namespace TFMS.Core.Model;

public class Sequence(IMediator mediator, string airportIdentifier, string[] feederFixes)
{
    readonly IMediator mediator = mediator;
    readonly List<Flight> arrivals = new();

    public string AirportIdentifier { get; } = airportIdentifier;
    public string[] FeederFixes { get; } = feederFixes;

    public IReadOnlyList<Flight> Arrivals => arrivals;

    public void Add(Flight flight)
    {
        if (arrivals.Any(c => c.Callsign == flight.Callsign))
        {
            throw new Exception($"{flight.Callsign} is already in the sequence for {AirportIdentifier}");
        }

        if (flight.DestinationIcaoCode != AirportIdentifier)
        {
            throw new Exception($"Cannot add {flight.Callsign} to sequence for {AirportIdentifier} as the destination is {flight.DestinationIcaoCode}");
        }

        // TODO: Additional validation

        arrivals.Add(flight);

        mediator.Publish(new SequenceModifiedNotification(this.ToDTO()));
    }
}
