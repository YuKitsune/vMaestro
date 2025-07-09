using MediatR;

namespace Maestro.Core.Messages;

public class FlightRemovedNotification(string airportIdentifier, string callsign) : INotification
{
    public string AirportIdentifier { get; } = airportIdentifier;
    public string Callsign { get; } = callsign;
}
