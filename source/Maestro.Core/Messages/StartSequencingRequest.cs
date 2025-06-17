using MediatR;

namespace Maestro.Core.Messages;

public class StartSequencingRequest(string airportIdentifier) : IRequest
{
    public string AirportIdentifier { get; } = airportIdentifier;
}