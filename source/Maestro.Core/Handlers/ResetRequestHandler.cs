using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;

namespace Maestro.Core.Handlers;

public class ResetRequestHandler : IRequestHandler<ResetRequest>
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly ISequenceProvider _sequenceProvider;
    readonly IMediator _mediator;

    public ResetRequestHandler(
        IAirportConfigurationProvider airportConfigurationProvider,
        ISequenceProvider sequenceProvider,
        IMediator mediator)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _sequenceProvider = sequenceProvider;
        _mediator = mediator;
    }

    public async Task Handle(ResetRequest request, CancellationToken cancellationToken)
    {
        foreach (var airportConfiguration in _airportConfigurationProvider.GetAirportConfigurations())
        {
            using var exclusiveSequence = await _sequenceProvider.GetSequence(
                airportConfiguration.Identifier,
                cancellationToken);

            exclusiveSequence.Sequence.Clear();

            await _mediator.Publish(
                new SequenceUpdatedNotification(
                    exclusiveSequence.Sequence.AirportIdentifier,
                    exclusiveSequence.Sequence.ToMessage()),
                cancellationToken);
        }
    }
}
