using MediatR;

namespace Maestro.Avalonia.Contracts;

public record OpenChangeFeederFixEstimateWindowRequest(
    string AirportIdentifier,
    string Callsign,
    string FeederFix,
    DateTimeOffset OriginalFeederFixEstimate)
    : IRequest;
