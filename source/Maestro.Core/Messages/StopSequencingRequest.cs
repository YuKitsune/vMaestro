using MediatR;

namespace Maestro.Core.Messages;

public class StopSequencingRequest(string airportIdentifier) : IRequest
{
    public string AirportIdentifier { get; } = airportIdentifier;
}
