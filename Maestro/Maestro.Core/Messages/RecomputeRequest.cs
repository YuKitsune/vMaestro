using MediatR;

namespace Maestro.Core.Messages;

public record RecomputeResponse;
public record RecomputeRequest(string AirportIdentifier, string Callsign) : IRequest<RecomputeResponse>;

public record ChangeRunwayResponse;
public record ChangeRunwayRequest(string AirportIdentifier, string Callsign, string RunwayIdentifier) : IRequest<ChangeRunwayResponse>;

public enum InsertionPoint
{
    Before,
    After
}
public record RemoveResponse;
public record RemoveRequest(string AirportIdentifier, string Callsign) : IRequest<RemoveResponse>;

public record DesequenceResponse;
public record DesequenceRequest(string AirportIdentifier, string Callsign) : IRequest<DesequenceResponse>;

public record MakePendingResponse;
public record MakePendingRequest(string AirportIdentifier, string Callsign) : IRequest<MakePendingResponse>;

public record ZeroDelayResponse;
public record ZeroDelayRequest(string AirportIdentifier, string Callsign) : IRequest<ZeroDelayResponse>;
