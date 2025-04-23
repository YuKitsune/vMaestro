using Maestro.Core.Configuration;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class ShutdownRequestHandler(
    IAirportConfigurationProvider airportConfigurationProvider,
    ISequenceProvider sequenceProvider,
    IMediator mediator)
    : IRequestHandler<ShutdownRequest, ShutdownResponse>
{
    public async Task<ShutdownResponse> Handle(ShutdownRequest request, CancellationToken cancellationToken)
    {
        var airportConfigs = airportConfigurationProvider.GetAirportConfigurations();
        foreach (var airportConfiguration in airportConfigs)
        {
            var sequence = sequenceProvider.TryGetSequence(airportConfiguration.Identifier);
            if (sequence is null)
                continue;

            // TODO:
            // await sequence.Stop(cancellationToken);
            
            await mediator.Publish(new SequenceModifiedNotification(sequence), cancellationToken);
        }
        
        return new ShutdownResponse();
    }
}