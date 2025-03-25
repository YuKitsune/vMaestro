using MediatR;
using TFMS.Core.DTOs;
using TFMS.Core.Model;

namespace TFMS.Core.Handlers;

public class InitializedNotificationHandler(SequenceProvider sequenceProvider, IMediator mediator) : INotificationHandler<InitializedNotification>
{
    readonly SequenceProvider _sequenceProvider = sequenceProvider;
    readonly IMediator _mediator = mediator;

    public Task Handle(InitializedNotification notification, CancellationToken cancellationToken)
    {
        foreach (var flightDataRecord in notification.FlightDataRecords)
        {
            // HACK
            _mediator.Publish(new FDRUpdatedNotification(flightDataRecord));
        }

        throw new NotImplementedException();
    }
}
