using MediatR;
using TFMS.Core.Dtos.Configuration;

namespace TFMS.Core.Dtos.Messages;

public record GetAirportConfigurationRequest : IRequest<GetAirportConfigurationResponse>;
public record GetAirportConfigurationResponse(AirportConfigurationDTO[] Airports);
