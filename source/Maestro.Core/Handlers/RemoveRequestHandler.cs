using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RemoveRequestHandler(ISequenceProvider sequenceProvider, ILogger logger)
    : IRequestHandler<RemoveRequest, RemoveResponse>
{
    public async Task<RemoveResponse> Handle(RemoveRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new RemoveResponse();
        }

        flight.Remove();
        flight.NeedsRecompute = true;

        // TODO: Re-calculate sequence

        return new RemoveResponse();
    }
}
