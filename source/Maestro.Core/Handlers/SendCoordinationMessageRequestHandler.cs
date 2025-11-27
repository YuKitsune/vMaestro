using Maestro.Core.Connectivity;
using Maestro.Core.Messages;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class SendCoordinationMessageRequestHandler(
    IMaestroConnectionManager connectionManager,
    ILogger logger)
    : IRequestHandler<SendCoordinationMessageRequest>
{
    public async Task Handle(SendCoordinationMessageRequest request, CancellationToken cancellationToken)
    {
        if (connectionManager.TryGetConnection(request.AirportIdentifier, out var connection) &&
            connection.IsConnected)
        {
            logger.Information("Sending coordination message {Message} to {AirportIdentifier}", request.Message, request.AirportIdentifier);
            await connection.Invoke(request, cancellationToken);
        }
    }
}
