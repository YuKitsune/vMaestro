using System.Collections.Concurrent;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using Microsoft.AspNetCore.SignalR;

namespace Maestro.Server;

public class MaestroHub(ILogger<MaestroHub> logger) : Hub
{
    private static readonly Dictionary<string, GroupKey> Connections = new();
    private static readonly ConcurrentDictionary<GroupKey, Sequence> Sequences = new();

    public async Task<JoinSequenceResponse> JoinSequence(JoinSequenceRequest request)
    {
        var groupKey = new GroupKey(request.Partition, request.AirportIdentifier);
        logger.LogInformation("Connection {ConnectionId} attempting to join {Group}",
            Context.ConnectionId, groupKey);

        // Validate this connection is not part of another sequence
        if (Connections.TryGetValue(Context.ConnectionId, out var existingGroupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to join {Group} but is already part of {ExistingGroup}",
                Context.ConnectionId, groupKey, existingGroupKey);
            throw new InvalidOperationException($"Connection {Context.ConnectionId} is already part of group {existingGroupKey}");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey.Value);
        Connections[Context.ConnectionId] = groupKey;

        var sequence = Sequences.GetOrAdd(groupKey, id => new Sequence(id));
        var wasEmpty = sequence.IsEmpty;
        var connection = sequence.AddConnection(Context.ConnectionId);

        logger.LogInformation("Connection {ConnectionId} successfully joined sequence {Group}. Total connections: {Count}",
            Context.ConnectionId,
            groupKey,
            sequence.Connections.Count);

        // TODO: Lookup callsign to determine the role

        // First connection becomes flow controller
        if (wasEmpty)
        {
            logger.LogInformation("Assigning {ConnectionId} as owner for {Group}",
                Context.ConnectionId, groupKey);
            connection.OwnsSequence = true;
        }

        return new JoinSequenceResponse(Context.ConnectionId, connection.OwnsSequence, sequence.LatestSequence);
    }

    public async Task LeaveSequence(LeaveSequenceRequest request)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to leave but is not part of any group", Context.ConnectionId);
            return;
        }

        if (request.AirportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "Connection {ConnectionId} attempted to leave {Group} but is not part of it.",
                Context.ConnectionId, groupKey);
            return;
        }

        logger.LogInformation("Connection {ConnectionId} attempting to leave {Group}",
            Context.ConnectionId, groupKey);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupKey.Value);

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        if (sequence != null)
        {
            var connection = sequence.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
            if (connection == null)
            {
                logger.LogWarning("Connection {ConnectionId} attempted to leave {Group} but was not found",
                    Context.ConnectionId, groupKey);
                Connections.Remove(Context.ConnectionId);
                return;
            }

            sequence.RemoveConnection(Context.ConnectionId);

            logger.LogInformation("Connection {ConnectionId} left {Group}. Remaining connections: {Count}",
                Context.ConnectionId, groupKey, sequence.Connections.Count);

            if (sequence.IsEmpty)
            {
                logger.LogInformation("Removing empty {Group}", groupKey);
                Sequences.TryRemove(groupKey, out _);
            }
            else if (connection.OwnsSequence)
            {
                // Re-assign ownership to the next connection if the owner left
                var newOwner = sequence.Connections.First();
                await Clients.Client(newOwner.Id).SendAsync("OwnershipGranted", new OwnershipGrantedNotification(groupKey.AirportIdentifier));
                newOwner.OwnsSequence = true;
            }
        }

        Connections.Remove(Context.ConnectionId);
    }

    public async Task SequenceUpdatedNotification(SequenceUpdatedNotification sequenceUpdatedNotification)
    {
        // Look up the group key for this connection
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to update sequence but is not part of any group", Context.ConnectionId);
            return;
        }

        if (sequenceUpdatedNotification.AirportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "Connection {ConnectionId} attempted to update {Airport} but is part of {Group}",
                Context.ConnectionId, sequenceUpdatedNotification.AirportIdentifier, groupKey);
            return;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        if (sequence == null)
        {
            logger.LogWarning(
                "Connection {ConnectionId} attempted to update {Group} but no sequence exists",
                Context.ConnectionId,
                groupKey);
            return;
        }

        var connection = sequence.Connections.Single(c => c.Id == Context.ConnectionId);
        if (!connection.OwnsSequence)
        {
            logger.LogWarning("Connection {ConnectionId} attempted to update {Group} but is not the owner",
                Context.ConnectionId,
                groupKey);
            return;
        }

        logger.LogDebug("{ConnectionId} updating {Group}",
            Context.ConnectionId, groupKey);

        sequence.LatestSequence = sequenceUpdatedNotification.Sequence;

        // Send to all clients in the group except the sender
        await Clients.GroupExcept(groupKey.Value, Context.ConnectionId)
            .SendAsync("SequenceUpdatedNotification", sequenceUpdatedNotification);
    }

    public async Task FlightUpdatedNotification(FlightUpdatedNotification flightUpdatedNotification)
    {
        await SendToFlowController(flightUpdatedNotification.Destination, "FlightUpdatedNotification", flightUpdatedNotification);
    }

    public async Task InsertFlight(InsertFlightRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "InsertFlightRequest", request);
    }

    public async Task InsertDeparture(InsertDepartureRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "InsertDepartureRequest", request);
    }

    public async Task MoveFlight(MoveFlightRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "MoveFlightRequest", request);
    }

    public async Task SwapFlights(SwapFlightsRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "SwapFlightsRequest", request);
    }

    public async Task RemoveFlight(RemoveRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "RemoveRequest", request);
    }

    public async Task DesequenceFlight(DesequenceRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "DesequenceRequest", request);
    }

    public async Task MakeFlightPending(MakePendingRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "MakePendingRequest", request);
    }

    public async Task MakeFlightStable(MakeStableRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "MakeStableRequest", request);
    }

    public async Task RecomputeFlight(RecomputeRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "RecomputeRequest", request);
    }

    public async Task ResumeSequencing(ResumeSequencingRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ResumeSequencingRequest", request);
    }

    public async Task ZeroDelay(ZeroDelayRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ZeroDelayRequest", request);
    }

    public async Task ChangeRunway(ChangeRunwayRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ChangeRunwayRequest", request);
    }

    public async Task ChangeRunwayMode(ChangeRunwayModeRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ChangeRunwayModeRequest", request);
    }

    public async Task ChangeFeederFixEstimate(ChangeFeederFixEstimateRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ChangeFeederFixEstimateRequest", request);
    }

    public async Task CreateSlot(CreateSlotRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "CreateSlotRequest", request);
    }

    public async Task ModifySlot(ModifySlotRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "ModifySlotRequest", request);
    }

    public async Task DeleteSlot(DeleteSlotRequest request)
    {
        await SendToFlowController(request.AirportIdentifier, "DeleteSlotRequest", request);
    }

    private async Task SendToFlowController<T>(string airportIdentifier, string messageType, T request)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MessageType} but is not part of any group",
                Context.ConnectionId, messageType);
            return;
        }

        if (airportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "Connection {ConnectionId} attempted to send {MessageType} for {Airport} but is part of {Group}",
                Context.ConnectionId, messageType, airportIdentifier, groupKey);
            return;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        if (sequence == null)
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MessageType} to owner of {Group} but no sequence exists",
                Context.ConnectionId, messageType, groupKey);
            return;
        }

        var flowController = sequence.Connections.SingleOrDefault(c => c.OwnsSequence);
        if (flowController == null)
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MessageType} to owner of {Group} but no owner exists",
                Context.ConnectionId, messageType, groupKey);
            return;
        }

        // Don't send messages to yourself
        if (flowController.Id == Context.ConnectionId)
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MessageType} to itself as owner of {Group}",
                Context.ConnectionId, messageType, groupKey);
            return;
        }

        logger.LogDebug("Connection {ConnectionId} sending {MessageType} to {FlowController} for {Group}",
            Context.ConnectionId, messageType, flowController.Id, groupKey);

        await Clients.Client(flowController.Id).SendAsync(messageType, request);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        if (exception is not null)
        {
            logger.LogError(exception, "Connection {ConnectionId} disconnected with error", connectionId);
        }
        else
        {
            logger.LogInformation("Connection {ConnectionId} disconnected", connectionId);
        }

        if (Connections.TryGetValue(connectionId, out var groupKey))
        {
            var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
            if (sequence != null)
            {
                await Groups.RemoveFromGroupAsync(connectionId, groupKey.Value);
                sequence.RemoveConnection(connectionId);

                logger.LogInformation("Connection {ConnectionId} left {Group}. Remaining connections: {Count}",
                    connectionId, groupKey, sequence.Connections.Count);

                if (sequence.IsEmpty)
                {
                    logger.LogInformation("Removing empty {Group}", groupKey);
                    Sequences.TryRemove(groupKey, out _);
                }
            }

            Connections.Remove(connectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("New connection established: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
}
