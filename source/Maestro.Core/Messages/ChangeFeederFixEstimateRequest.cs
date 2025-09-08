using MediatR;

namespace Maestro.Core.Messages;

public record ChangeFeederFixEstimateRequest(
    string AirportIdentifier,
    string Callsign,
    DateTimeOffset NewFeederFixEstimate) : IRequest, ISynchronizedMessage;
