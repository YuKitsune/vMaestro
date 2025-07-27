using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ZeroDelayRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<ZeroDelayRequest, ZeroDelayResponse>
{
    public async Task<ZeroDelayResponse> Handle(ZeroDelayRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new ZeroDelayResponse();
        }

        flight.NoDelay = true;

        // TODO: Reallocate position in sequence to ensure no delay is applied

        await mediator.Publish(
            new SequenceChangedNotification(lockedSequence.Sequence.ToDto()),
            cancellationToken);

        return new ZeroDelayResponse();
    }
}
