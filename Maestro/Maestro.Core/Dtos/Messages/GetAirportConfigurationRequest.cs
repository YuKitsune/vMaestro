using Maestro.Core.Dtos.Configuration;
using MediatR;

namespace Maestro.Core.Dtos.Messages;

public record GetAirportConfigurationRequest : IRequest<GetAirportConfigurationResponse>;
public record GetAirportConfigurationResponse(AirportConfigurationDTO[] Airports);
