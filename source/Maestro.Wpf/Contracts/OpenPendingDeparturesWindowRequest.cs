using MediatR;

namespace Maestro.Wpf.Contracts;

public record OpenPendingDeparturesWindowRequest(string AirportIdentifier) : IRequest;
