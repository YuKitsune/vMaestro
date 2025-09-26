using Maestro.Core.Configuration;

namespace Maestro.Core.Infrastructure;

public interface IMaestroConnectionFactory
{
    MaestroConnection Create(string partition, string airportIdentifier, string callsign, Role role);
}
