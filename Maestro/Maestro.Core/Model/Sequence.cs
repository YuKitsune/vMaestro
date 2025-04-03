using Maestro.Core.Dtos.Messages;
using MediatR;

namespace Maestro.Core.Model;

public class Sequence(IMediator mediator, string airportIdentifier, string[] feederFixes)
{
    readonly IMediator _mediator = mediator;
    readonly List<Flight> _arrivals = [];

    public string AirportIdentifier { get; } = airportIdentifier;
    public string[] FeederFixes { get; } = feederFixes;

    public IReadOnlyList<Flight> Arrivals => _arrivals;

    public void Add(Flight flight)
    {
        if (_arrivals.Any(c => c.Callsign == flight.Callsign))
        {
            throw new Exception($"{flight.Callsign} is already in the sequence for {AirportIdentifier}");
        }

        if (flight.DestinationIcaoCode != AirportIdentifier)
        {
            throw new Exception($"Cannot add {flight.Callsign} to sequence for {AirportIdentifier} as the destination is {flight.DestinationIcaoCode}");
        }

        // TODO: Additional validation

        _arrivals.Add(flight);

        _mediator.Publish(new SequenceModifiedNotification(this.ToDto()));
    }
}
