using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ChangeRunwayRequestHandler(ISequenceProvider sequenceProvider, IMediator mediator, ILogger logger)
    : IRequestHandler<ChangeRunwayRequest, ChangeRunwayResponse>
{
    public async Task<ChangeRunwayResponse> Handle(ChangeRunwayRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.TryGetFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new ChangeRunwayResponse();
        }

        flight.SetRunway(request.RunwayIdentifier, true);
        flight.NeedsRecompute = true;

        return new ChangeRunwayResponse();
    }
}
