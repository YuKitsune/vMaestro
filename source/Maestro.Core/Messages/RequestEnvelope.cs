using Maestro.Core.Configuration;

namespace Maestro.Core.Messages;

public class RequestEnvelope<T>
{
    public required string OriginatingCallsign { get; init; }
    public required string OriginatingConnectionId { get; init; }
    public required Role OriginatingRole { get; init; }
    public required T Request { get; init; }
}

public static class RequestEnvelopeHelper
{
    public static RequestEnvelope<T> CreateEnvelope<T>(T request, string callsign, string connectionId, Role role)
    {
        return new RequestEnvelope<T>
        {
            OriginatingCallsign = callsign,
            OriginatingConnectionId = connectionId,
            OriginatingRole = role,
            Request = request
        };
    }
    
    public static T UnwrapRequest<T>(RequestEnvelope<T> envelope)
    {
        return envelope.Request;
    }
    
    public static (T request, string callsign, string connectionId, Role role) UnwrapFull<T>(RequestEnvelope<T> envelope)
    {
        return (envelope.Request, envelope.OriginatingCallsign, envelope.OriginatingConnectionId, envelope.OriginatingRole);
    }
}