using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenChangeFeederFixEstimateWindowRequest(
    string AirportIdentifier,
    string Callsign,
    string FeederFix,
    DateTimeOffset OriginalFeederFixEstimate)
    : IRequest;
