using Maestro.Core.Configuration;
using Maestro.Core.Hosting;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Mocks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serilog;

namespace Maestro.Core.Tests.Builders;

public class InstanceBuilder(AirportConfiguration airportConfiguration)
{
    readonly SequenceBuilder _sequenceBuilder = new(airportConfiguration);

    public InstanceBuilder WithSequence(Action<SequenceBuilder> configure)
    {
        configure(_sequenceBuilder);
        return this;
    }

    public (IMaestroInstanceManager, MaestroInstance, Session, Sequence) Build()
    {
        var sequence = _sequenceBuilder.Build();
        var session = new Session(sequence);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IAirportConfigurationProvider>(new AirportConfigurationProvider([airportConfiguration]));
        serviceCollection.AddSingleton(Substitute.For<IClock>());
        serviceCollection.AddSingleton(Substitute.For<ILogger>());
        serviceCollection.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MaestroInstance>());
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var instance = new MaestroInstance(airportConfiguration.Identifier, session, mediator);
        var instanceManager = new MockInstanceManager(instance);

        return (instanceManager, instance, session, sequence);
    }
}
