using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class DesequenceRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<DesequenceRequest, DesequenceResponse>
{
    public async Task<DesequenceResponse> Handle(DesequenceRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindFlight(request.Callsign);
        if (flight is null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new DesequenceResponse();
        }

        lockedSequence.Sequence.DesequenceFlight(request.Callsign);

        // TODO: Publish Desequenced notification instead
        await mediator.Publish(
            new SequenceChangedNotification(lockedSequence.Sequence.ToDto()),
            cancellationToken);

        // TODO: Re-calculate sequence

        return new DesequenceResponse();
    }
}
