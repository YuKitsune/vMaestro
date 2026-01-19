using Maestro.Core.Configuration;
using Maestro.Core.Connectivity;
using Maestro.Core.Connectivity.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RelayRequestHandler(IMaestroConnectionManager connectionManager, ServerConfiguration serverConfiguration, IMediator mediator, ILogger logger)
    : IRequestHandler<RelayRequest, ServerResponse>
{
    public async Task<ServerResponse> Handle(RelayRequest request, CancellationToken cancellationToken)
    {
        var envelope = request.Envelope;
        var actionKey = request.ActionKey;

        logger.Information("Processing {ActionKey} from {Callsign} (Role: {Role}) for airport {Airport}",
            actionKey, envelope.OriginatingCallsign, envelope.OriginatingRole, envelope.Request.AirportIdentifier);

        var airportIdentifier = envelope.Request.AirportIdentifier;
        if (string.IsNullOrEmpty(airportIdentifier))
        {
            logger.Warning("Could not determine airport identifier from request {RequestType}", request.Envelope.Request.GetType().Name);
            return ServerResponse.CreateFailure("Could not determine airport identifier from request");
        }

        if (!connectionManager.TryGetConnection(airportIdentifier, out var connection))
            return ServerResponse.CreateFailure($"Could not find connection for {airportIdentifier}");

        // Check if the originating user has permission to perform this action
        // TODO: Consider moving this into each handler, and adding the sender details to all requests
        if (!CanPerformAction(connection, envelope.OriginatingRole, actionKey))
        {
            logger.Warning("{Callsign} attempted {ActionKey} but does not have permission (Role: {Role})",
                envelope.OriginatingCallsign, actionKey, envelope.OriginatingRole);

            return ServerResponse.CreateFailure($"{envelope.OriginatingRole} cannot perform {actionKey}");
        }

        // Permission granted - unwrap and forward the request to the appropriate handler
        logger.Information("{Callsign} authorized for {ActionKey}, forwarding to handler",
            envelope.OriginatingCallsign, actionKey);

        try
        {
            await mediator.Send(envelope.Request, cancellationToken);
            return ServerResponse.CreateSuccess();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to process {ActionKey} from {Callsign}", actionKey, envelope.OriginatingCallsign);
            return ServerResponse.CreateFailure($"Failed to process {actionKey}: {ex.Message}");
        }
    }

    bool CanPerformAction(IMaestroConnection connection, Role userRole, string actionKey)
    {
        // If we're the flow controller, we need to enforce permission checks
        if (connection is { IsMaster: true, Role: Role.Flow })
        {
            var permissions = serverConfiguration.Permissions;
            return permissions.TryGetValue(actionKey, out var allowedRoles) && allowedRoles.Contains(userRole);
        }

        // Otherwise, allow all actions
        return true;
    }
}
