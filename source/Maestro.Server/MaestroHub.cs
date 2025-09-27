using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Server.Handlers;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using ILogger = Serilog.ILogger;

namespace Maestro.Server;

public class MaestroHub(IMediator mediator, ILogger logger) : Hub
{
    public async Task SequenceUpdated(SequenceUpdatedNotification sequenceUpdatedNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<SequenceUpdatedNotification>(Context.ConnectionId, sequenceUpdatedNotification));
    }

    public async Task FlightUpdated(FlightUpdatedNotification flightUpdatedNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<FlightUpdatedNotification>(Context.ConnectionId, flightUpdatedNotification));
    }

    public async Task Information(InformationNotification informationNotification)
    {
        await mediator.Publish(new NotificationContextWrapper<InformationNotification>(Context.ConnectionId, informationNotification));
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

    public async Task<RelayResponse> ZeroDelay(ZeroDelayRequest request)
    {
        return await RelayToMaster("ZeroDelay", request);
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

    async Task<RelayResponse> RelayToMaster<T>(string methodName, T request)
    {
        return await mediator.Send(
            new RequestContextWrapper<RelayToMasterRequest<T>, RelayResponse>(
                Context.ConnectionId,
                new RelayToMasterRequest<T>(methodName, request)));
    }

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        if (httpContext is null)
        {
            throw new Exception("HttpContext is null");
        }

        // TODO: HTTP 400 if missing parameters

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

        logger.Information("{ConnectionId} connected", Context.ConnectionId);

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
