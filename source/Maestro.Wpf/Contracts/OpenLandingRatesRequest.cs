using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenLandingRatesRequest(string AirportIdentifier) : IRequest;
