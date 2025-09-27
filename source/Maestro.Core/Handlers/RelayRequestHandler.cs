using Maestro.Core.Configuration;
using Maestro.Core.Sessions;
using MediatR;
using Serilog;

namespace Maestro.Core.Handlers;

public class RelayRequestHandler(ISessionManager sessionManager, ServerConfiguration serverConfiguration, IMediator mediator, ILogger logger)
    : IRequestHandler<RelayRequest, RelayResponse>
{
    public async Task<RelayResponse> Handle(RelayRequest request, CancellationToken cancellationToken)
    {
        var envelope = request.Envelope;
        var actionKey = request.ActionKey;

        logger.Information("Processing {ActionKey} from {Callsign} (Role: {Role}) for airport {Airport}",
            actionKey, envelope.OriginatingCallsign, envelope.OriginatingRole, GetAirportIdentifier(envelope.Request));

        // Get the airport identifier from the wrapped request
        var airportIdentifier = GetAirportIdentifier(envelope.Request);
        if (string.IsNullOrEmpty(airportIdentifier))
        {
            logger.Warning("Could not determine airport identifier from request {RequestType}", request.Envelope.Request.GetType().Name);
            return RelayResponse.CreateFailure("Could not determine airport identifier from request");
        }

        using (var lockedSession = await sessionManager.AcquireSession(airportIdentifier, cancellationToken))
        {
            var session = lockedSession.Session;

            // Check if the originating user has permission to perform this action
            if (!CanPerformAction(session, envelope.OriginatingRole, actionKey))
            {
                logger.Warning("{Callsign} attempted {ActionKey} but does not have permission (Role: {Role})",
                    envelope.OriginatingCallsign, actionKey, envelope.OriginatingRole);

                return RelayResponse.CreateFailure($"{envelope.OriginatingRole} cannot perform {actionKey}");
            }

            // Permission granted - unwrap and forward the request to the appropriate handler
            logger.Information("{Callsign} authorized for {ActionKey}, forwarding to handler",
                envelope.OriginatingCallsign, actionKey);
        }

        try
        {
            await mediator.Send(envelope.Request, cancellationToken);
            return RelayResponse.CreateSuccess();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to process {ActionKey} from {Callsign}", actionKey, envelope.OriginatingCallsign);
            return RelayResponse.CreateFailure($"Failed to process {actionKey}: {ex.Message}");
        }
    }

    bool CanPerformAction(ISession session, Role userRole, string actionKey)
    {
        // If we're the flow controller, we need to enforce permission checks
        if (session is { OwnsSequence: true, Role: Role.Flow })
        {
            var permissions = serverConfiguration.Permissions;
            return permissions.TryGetValue(actionKey, out var allowedRoles) && allowedRoles.Contains(userRole);
        }

        // Otherwise, allow all actions
        return true;
    }

    static string GetAirportIdentifier(IRequest request)
    {
        // Use reflection to get the AirportIdentifier property from the request
        var property = request.GetType().GetProperty("AirportIdentifier");
        return property?.GetValue(request) as string ?? string.Empty;
    }
}
