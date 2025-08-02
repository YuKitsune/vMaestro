using Maestro.Core.Messages;
using Maestro.Core.Model;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class MakePendingRequestHandler(ISequenceProvider sequenceProvider, ILogger logger)
    : IRequestHandler<MakePendingRequest, MakePendingResponse>
{
    public async Task<MakePendingResponse> Handle(MakePendingRequest request, CancellationToken cancellationToken)
    {
        using var lockedSequence = await sequenceProvider.GetSequence(request.AirportIdentifier, cancellationToken);

        var flight = lockedSequence.Sequence.FindTrackedFlight(request.Callsign);
        if (flight == null)
        {
            logger.Warning("Flight {Callsign} not found for airport {AirportIdentifier}.", request.Callsign, request.AirportIdentifier);
            return new MakePendingResponse();
        }

        throw new NotImplementedException("Make Pending not yet implemented.");
    }
}
