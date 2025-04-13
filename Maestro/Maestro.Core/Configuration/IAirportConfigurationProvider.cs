using Maestro.Core.Dtos.Configuration;

namespace Maestro.Core.Configuration;

public interface IAirportConfigurationProvider
{
    AirportConfiguration[] GetAirportConfigurations();
}
