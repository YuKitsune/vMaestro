using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenChangeFeederFixEstimateWindowRequest(
    string AirportIdentifier,
    string Callsign,
    string FeederFix,
    DateTimeOffset OriginalFeederFixEstimate)
    : IRequest;
