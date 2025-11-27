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

    public async Task<ServerResponse> SendCoordinationMessage(SendCoordinationMessageRequest coordinationRequest)
    {
        return await mediator.Send(new RequestContextWrapper<SendCoordinationMessageRequest, ServerResponse>(Context.ConnectionId, coordinationRequest));
    }

    public async Task<ServerResponse> InsertFlight(InsertFlightRequest request)
    {
        return await RelayToMaster("InsertFlight", request);
    }

    public async Task<ServerResponse> InsertOvershoot(InsertOvershootRequest request)
    {
        return await RelayToMaster("InsertOvershoot", request);
    }

    public async Task<ServerResponse> InsertDeparture(InsertDepartureRequest request)
    {
        return await RelayToMaster("InsertDeparture", request);
    }

    public async Task<ServerResponse> MoveFlight(MoveFlightRequest request)
    {
        return await RelayToMaster("MoveFlight", request);
    }

    public async Task<ServerResponse> SwapFlights(SwapFlightsRequest request)
    {
        return await RelayToMaster("SwapFlights", request);
    }

    public async Task<ServerResponse> Remove(RemoveRequest request)
    {
        return await RelayToMaster("Remove", request);
    }

    public async Task<ServerResponse> Desequence(DesequenceRequest request)
    {
        return await RelayToMaster("Desequence", request);
    }

    public async Task<ServerResponse> MakePending(MakePendingRequest request)
    {
        return await RelayToMaster("MakePending", request);
    }

    public async Task<ServerResponse> MakeStable(MakeStableRequest request)
    {
        return await RelayToMaster("MakeStable", request);
    }

    public async Task<ServerResponse> Recompute(RecomputeRequest request)
    {
        return await RelayToMaster("Recompute", request);
    }

    public async Task<ServerResponse> ResumeSequencing(ResumeSequencingRequest request)
    {
        return await RelayToMaster("ResumeSequencing", request);
    }

    public async Task<ServerResponse> ManualDelay(ManualDelayRequest request)
    {
        return await RelayToMaster("ManualDelay", request);
    }

    public async Task<ServerResponse> ChangeRunway(ChangeRunwayRequest request)
    {
        return await RelayToMaster("ChangeRunway", request);
    }

    public async Task<ServerResponse> ChangeRunwayMode(ChangeRunwayModeRequest request)
    {
        return await RelayToMaster("ChangeRunwayMode", request);
    }

    public async Task<ServerResponse> ChangeFeederFixEstimate(ChangeFeederFixEstimateRequest request)
    {
        return await RelayToMaster("ChangeFeederFixEstimate", request);
    }

    public async Task<ServerResponse> CreateSlot(CreateSlotRequest request)
    {
        return await RelayToMaster("CreateSlot", request);
    }

    public async Task<ServerResponse> ModifySlot(ModifySlotRequest request)
    {
        return await RelayToMaster("ModifySlot", request);
    }

    public async Task<ServerResponse> DeleteSlot(DeleteSlotRequest request)
    {
        return await RelayToMaster("DeleteSlot", request);
    }

    async Task<ServerResponse> RelayToMaster(string methodName, IRequest request)
    {
        return await mediator.Send(
            new RequestContextWrapper<RelayToMasterRequest, ServerResponse>(
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
            throw new HubException("Connection rejected: Client version not provided");
        }

        if (!VersionCompatibility.IsCompatible(clientVersion, ServerVersion))
        {
            logger.Warning(
                "{ConnectionId} attempted to connect with incompatible version {ClientVersion} (server version: {ServerVersion})",
                Context.ConnectionId, clientVersion, ServerVersion);
            throw new HubException($"Incompatible version. Client version: {clientVersion}, Server version: {ServerVersion}");
        }

        var partition = httpContext.Request.Query["partition"].FirstOrDefault();
        if (string.IsNullOrEmpty(partition))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty partition", Context.ConnectionId);
            throw new HubException("Connection rejected: Partition not provided");
        }

        var airportIdentifier = httpContext.Request.Query["airportIdentifier"].FirstOrDefault();
        if (string.IsNullOrEmpty(airportIdentifier))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty airport identifier", Context.ConnectionId);
            throw new HubException("Connection rejected: Airport identifier not provided");
        }

        var callsign = httpContext.Request.Query["callsign"].FirstOrDefault();
        if (string.IsNullOrEmpty(callsign))
        {
            logger.Warning("{ConnectionId} attempted to connect with an empty callsign", Context.ConnectionId);
            throw new HubException("Connection rejected: Callsign not provided");
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
