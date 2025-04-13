using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Messages;

public record GetAirportConfigurationRequest : IRequest<GetAirportConfigurationResponse>;
public record GetAirportConfigurationResponse(AirportConfiguration[] Airports);
