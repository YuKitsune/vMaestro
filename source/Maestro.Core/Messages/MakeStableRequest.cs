using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record MakeStableRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
