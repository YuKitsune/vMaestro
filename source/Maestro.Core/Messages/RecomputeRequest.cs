using MediatR;

namespace Maestro.Core.Messages;

public record RecomputeRequest(string AirportIdentifier, string Callsign) : IRequest, ISynchronizedMessage;
