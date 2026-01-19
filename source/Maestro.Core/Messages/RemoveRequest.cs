using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record RemoveRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
