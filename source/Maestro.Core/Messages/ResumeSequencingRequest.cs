using Maestro.Core.Connectivity.Contracts;
using MediatR;

namespace Maestro.Core.Messages;

public record ResumeSequencingRequest(string AirportIdentifier, string Callsign) : IRequest, IRelayableRequest;
