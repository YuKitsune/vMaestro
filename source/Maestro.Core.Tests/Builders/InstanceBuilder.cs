using Maestro.Core.Configuration;
using Maestro.Core.Hosting;
using Maestro.Core.Model;
using Maestro.Core.Sessions;
using Maestro.Core.Tests.Mocks;

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
        var instance = new MaestroInstance(airportConfiguration.Identifier, session);
        var instanceManager = new MockInstanceManager(instance);

        return (instanceManager, instance, session, sequence);
    }
}
