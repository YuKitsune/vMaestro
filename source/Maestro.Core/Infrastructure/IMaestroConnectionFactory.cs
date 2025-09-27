namespace Maestro.Core.Infrastructure;

public interface IMaestroConnectionFactory
{
    MaestroConnection Create(string partition, string airportIdentifier, string callsign);
}
