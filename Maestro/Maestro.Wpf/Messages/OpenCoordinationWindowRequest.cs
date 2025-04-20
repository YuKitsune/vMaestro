using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenCoordinationWindowResponse;
public record OpenCoordinationWindowRequest(string AirportIdentifier, string Callsign) : IRequest<OpenCoordinationWindowResponse>;