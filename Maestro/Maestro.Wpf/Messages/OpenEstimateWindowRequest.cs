using MediatR;

namespace Maestro.Wpf.Messages;

public record OpenEstimateWindowResponse;
public record OpenEstimateWindowRequest(string AirportIdentifier, string Callsign) : IRequest<OpenEstimateWindowResponse>;