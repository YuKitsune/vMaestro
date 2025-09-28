using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Serilog;

namespace Maestro.Core.Sessions;

public interface ISessionFactory
{
    Session Create(Sequence sequence);
}

// TODO: Use Autofac to resolve dependencies
public class SessionFactory(IMaestroConnectionFactory maestroConnectionFactory) : ISessionFactory
{
    public Session Create(Sequence sequence)
    {
        return new Session(sequence, maestroConnectionFactory);
    }
}
