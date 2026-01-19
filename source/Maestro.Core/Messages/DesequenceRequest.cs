using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record DesequenceRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
