using MediatR;

namespace Maestro.Core.Messages;

public record RecomputeResponse;
public record RecomputeRequest(string AirportIdentifier, string Callsign) : IRequest<RecomputeResponse>;
