namespace Maestro.Core.Infrastructure;

public interface IMaestroConnectionFactory
{
    MaestroConnection Create(string airportIdentifier, string partition);
}
