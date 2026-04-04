using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Mocks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serilog;

namespace Maestro.Core.Tests.Builders;

public class SessionBuilder(AirportConfiguration airportConfiguration)
{
    readonly SequenceBuilder _sequenceBuilder = new(airportConfiguration);

    public SessionBuilder WithSequence(Action<SequenceBuilder> configure)
    {
        configure(_sequenceBuilder);
        return this;
    }

    public (ISessionManager, Session, Sequence) Build()
    {
        var sequence = _sequenceBuilder.Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAirportConfigurationProvider>(new AirportConfigurationProvider([airportConfiguration]));
        serviceCollection.AddSingleton(Substitute.For<IClock>());
        var logger = Substitute.For<ILogger>();
        serviceCollection.AddSingleton(logger);
        serviceCollection.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Session>());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var session = new Session(sequence, mediator, logger);
        var sessionManager = new MockSessionManager(session);

        return (sessionManager, session, sequence);
    }
}
