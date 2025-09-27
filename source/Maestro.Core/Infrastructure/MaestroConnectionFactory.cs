using Maestro.Core.Configuration;
using MediatR;
using Serilog;

namespace Maestro.Core.Infrastructure;

public class MaestroConnectionFactory(
    ServerConfiguration serverConfiguration,
    IMediator mediator,
    ILogger logger)
    : IMaestroConnectionFactory
{
    public MaestroConnection Create(string partition, string airportIdentifier, string callsign)
    {
        return new MaestroConnection(
            serverConfiguration,
            partition,
            airportIdentifier,
            callsign,
            mediator,
            logger.ForContext<MaestroConnection>());
    }
}
