using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record RecomputeRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
