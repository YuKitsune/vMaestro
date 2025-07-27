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

            var notifications = new List<INotification>
            {
                new SequenceChangedNotification(exclusiveSequence.Sequence.ToDto()),
                new RunwayModeChangedNotification(
                    airportConfiguration.Identifier,
                    exclusiveSequence.Sequence.CurrentRunwayMode.ToMessage(),
                    null,
                    default)
            };

            foreach (var notification in notifications)
            {
                await _mediator.Publish(notification, cancellationToken);
            }
        }
    }
}
