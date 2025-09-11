using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions;

public interface ISessionFactory
{
    Session Create(Sequence sequence, MaestroConnection? connection = null);
}

// TODO: Use Autofac to resolve dependencies
public class SessionFactory(
    IAirportConfigurationProvider airportConfigurationProvider,
    IMediator mediator,
    ILogger logger)
    : ISessionFactory
{
    public Session Create(Sequence sequence, MaestroConnection? connection = null)
    {
        return new Session(airportConfigurationProvider, mediator, logger, sequence, connection);
    }
}
