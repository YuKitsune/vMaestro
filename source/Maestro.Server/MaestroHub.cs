using Maestro.Core;
using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server;

public class MaestroHub(IMediator mediator, ILogger logger) : Hub
{
    static readonly string ServerVersion = AssemblyVersionHelper.GetVersion(typeof(MaestroHub).Assembly);

    public async Task SessionUpdated(SessionUpdatedNotification sessionUpdatedNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<SessionUpdatedNotification>(Context.ConnectionId, sessionUpdatedNotification));
    }

    public async Task FlightUpdated(FlightUpdatedNotification flightUpdatedNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<FlightUpdatedNotification>(Context.ConnectionId, flightUpdatedNotification));
    }

    public async Task Coordination(CoordinationNotification coordinationNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<CoordinationNotification>(Context.ConnectionId, coordinationNotification));
    }

    public async Task<RelayResponse> InsertFlight(InsertFlightRequest request)
    {
        return await RelayToMaster("InsertFlight", request);
    }

    public async Task<RelayResponse> InsertOvershoot(InsertOvershootRequest request)
    {
        return await RelayToMaster("InsertOvershoot", request);
    }

    public async Task<RelayResponse> InsertDeparture(InsertDepartureRequest request)
    {
        return await RelayToMaster("InsertDeparture", request);
    }

    public async Task<RelayResponse> MoveFlight(MoveFlightRequest request)
    {
        return await RelayToMaster("MoveFlight", request);
    }

    public async Task<RelayResponse> SwapFlights(SwapFlightsRequest request)
    {
        return await RelayToMaster("SwapFlights", request);
    }

    public async Task<RelayResponse> Remove(RemoveRequest request)
    {
        return await RelayToMaster("Remove", request);
    }

    public async Task<RelayResponse> Desequence(DesequenceRequest request)
    {
        return await RelayToMaster("Desequence", request);
    }

    public async Task<RelayResponse> MakePending(MakePendingRequest request)
    {
        return await RelayToMaster("MakePending", request);
    }

    public async Task<RelayResponse> MakeStable(MakeStableRequest request)
    {
        return await RelayToMaster("MakeStable", request);
    }

    public async Task<RelayResponse> Recompute(RecomputeRequest request)
    {
        return await RelayToMaster("Recompute", request);
    }

    public async Task<RelayResponse> ResumeSequencing(ResumeSequencingRequest request)
    {
        return await RelayToMaster("ResumeSequencing", request);
    }

    public async Task<RelayResponse> ManualDelay(ManualDelayRequest request)
    {
        return await RelayToMaster("ManualDelay", request);
    }

    public async Task<RelayResponse> ChangeRunway(ChangeRunwayRequest request)
    {
        return await RelayToMaster("ChangeRunway", request);
    }

    public async Task<RelayResponse> ChangeRunwayMode(ChangeRunwayModeRequest request)
    {
        return await RelayToMaster("ChangeRunwayMode", request);
    }

    public async Task<RelayResponse> ChangeFeederFixEstimate(ChangeFeederFixEstimateRequest request)
    {
        return await RelayToMaster("ChangeFeederFixEstimate", request);
    }

    public async Task<RelayResponse> CreateSlot(CreateSlotRequest request)
    {
        return await RelayToMaster("CreateSlot", request);
    }

    public async Task<RelayResponse> ModifySlot(ModifySlotRequest request)
    {
        return await RelayToMaster("ModifySlot", request);
    }

    public async Task<RelayResponse> DeleteSlot(DeleteSlotRequest request)
    {
        return await RelayToMaster("DeleteSlot", request);
    }

    async Task<RelayResponse> RelayToMaster(string methodName, IRequest request)
    {
        return await mediator.Send(
            new RequestContextWrapper<RelayToMasterRequest, RelayResponse>(
                Context.ConnectionId,
                new RelayToMasterRequest(methodName, request)));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            throw new Exception("HttpContext is null");
        }

        // TODO: HTTP 400 if missing parameters

        var clientVersion = httpContext.Request.Query["version"].FirstOrDefault();
        if (string.IsNullOrEmpty(clientVersion))
        {
            logger.Warning("{ConnectionId} attempted to connect without a version", Context.ConnectionId);
            Context.Abort();
            return;
        }

        if (!VersionCompatibility.IsCompatible(clientVersion, ServerVersion))
        {
            logger.Warning(
                "{ConnectionId} attempted to connect with incompatible version {ClientVersion} (server version: {ServerVersion})",
                Context.ConnectionId, clientVersion, ServerVersion);
            Context.Abort();
            return;
        }

        var partition = httpContext.Request.Query["partition"].FirstOrDefault();
        if (string.IsNullOrEmpty(partition))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty partition", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var airportIdentifier = httpContext.Request.Query["airportIdentifier"].FirstOrDefault();
        if (string.IsNullOrEmpty(airportIdentifier))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty airport identifier", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var callsign = httpContext.Request.Query["callsign"].FirstOrDefault();
        if (string.IsNullOrEmpty(callsign))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty callsign", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var roleString = httpContext.Request.Query["role"].FirstOrDefault();
        if (!Enum.TryParse<Role>(roleString, out var role))
        {
            logger.Warning("{ConnectionId} attempted to connect with an invalid role {Role}", Context.ConnectionId, roleString);
            Context.Abort();
            return;
        }

        logger.Information("{ConnectionId} connected with version {ClientVersion}", Context.ConnectionId, clientVersion);

        var request = new ConnectRequest(
            partition,
            airportIdentifier,
            callsign,
            role);

        await mediator.Send(new RequestContextWrapper<ConnectRequest>(Context.ConnectionId, request));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            logger.Error(exception, "{ConnectionId} disconnected", Context.ConnectionId);
        }
        else
        {
            logger.Information("{ConnectionId} disconnected", Context.ConnectionId);
        }

        await mediator.Publish(new ClientDisconnectedNotification(Context.ConnectionId));
        await base.OnDisconnectedAsync(exception);
    }
}
