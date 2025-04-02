using TFMS.Core.Dtos.Configuration;

namespace TFMS.Core.Configuration;

public interface IAirportConfigurationProvider
{
    AirportConfigurationDTO[] GetAirportConfigurations();
}
