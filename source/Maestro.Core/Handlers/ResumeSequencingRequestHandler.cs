using Maestro.Core.Connectivity;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class ResumeSequencingRequestHandler(
    ISessionManager sessionManager,
    IMaestroConnectionManager connectionManager,
    IMediator mediator,
    ILogger logger)
    : IRequestHandler<ResumeSequencingRequest>
{
    public async Task Handle(ResumeSequencingRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected &&
            !connection.IsMaster)
        {
            logger.Information("Relaying ResumeSequencingRequest for {AirportIdentifier}", request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
            return;
        }

        using (var lockedSession = await sessionManager.AcquireSession(request.AirportIdentifier, cancellationToken))
        {
            var sequence = lockedSession.Session.Sequence;
            sequence.Resume(request.Callsign);

            logger.Information("Flight {Callsign} resumed for {AirportIdentifier}", request.Callsign, request.AirportIdentifier);
        }

        // Let the RecomputeRequestHandler do the scheduling and notification
        await mediator.Send(new RecomputeRequest(request.AirportIdentifier, request.Callsign), cancellationToken);
    }
}
