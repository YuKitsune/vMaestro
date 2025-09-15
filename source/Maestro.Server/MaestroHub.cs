using System.Collections.Concurrent;
using Maestro.Core.Configuration;
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
        logger.LogInformation("{Callsign} attempting to join {Group}",
            request.Position, groupKey);

        // Validate this connection is not part of another sequence
        if (Connections.TryGetValue(Context.ConnectionId, out var existingGroupKey))
        {
            logger.LogWarning("{Callsign} attempted to join {Group} but is already part of {ExistingGroup}",
                request.Position, groupKey, existingGroupKey);
            throw new InvalidOperationException($"{request.Position} is already part of group {existingGroupKey}");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, groupKey.Value);
        Connections[Context.ConnectionId] = groupKey;

        var sequence = Sequences.GetOrAdd(groupKey, id => new Sequence(id));
        var wasEmpty = sequence.IsEmpty;
        var connection = sequence.AddConnection(Context.ConnectionId, request.Position, request.Role);

        logger.LogInformation("{Callsign} successfully joined {Group}. Total connections: {Count}",
            request.Position,
            groupKey,
            sequence.Connections.Count);

        // TODO: What if there are two flow controllers?
        if (request.Role == Role.Flow)
        {
            // Revoke ownership from current owner if any
            var currentOwner = sequence.Connections.FirstOrDefault(c => c.OwnsSequence && c.Id != Context.ConnectionId);
            if (currentOwner != null)
            {
                logger.LogInformation("Revoking ownership from {PreviousCallsign} for {Group}",
                    currentOwner.Callsign, groupKey);
                currentOwner.OwnsSequence = false;
                await Clients.Client(currentOwner.Id).SendAsync("OwnershipRevoked", new OwnershipRevokedNotification(groupKey.AirportIdentifier));
            }

            logger.LogInformation("Assigning {Callsign} as flow controller for {Group}",
                request.Position, groupKey);
            connection.OwnsSequence = true;
        }
        else if (wasEmpty)
        {
            // First connection becomes flow controller if no FMP controller is present
            logger.LogInformation("Assigning {Callsign} as owner for {Group} (first connection)",
                request.Position, groupKey);
            connection.OwnsSequence = true;
        }

        return new JoinSequenceResponse(Context.ConnectionId, connection.OwnsSequence, sequence.LatestSequence, PermissionHelper.FullAccess());
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

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var connection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var callsign = connection?.Callsign ?? "Unknown";

        logger.LogInformation("{Callsign} attempting to leave {Group}",
            callsign, groupKey);

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupKey.Value);

        if (sequence != null)
        {
            if (connection == null)
            {
                logger.LogWarning("Connection {ConnectionId} attempted to leave {Group} but was not found",
                    Context.ConnectionId, groupKey);
                Connections.Remove(Context.ConnectionId);
                return;
            }

            var wasFlowController = connection.Role == Role.Flow;
            sequence.RemoveConnection(Context.ConnectionId);

            logger.LogInformation("{Callsign} left {Group}. Remaining connections: {Count}",
                callsign, groupKey, sequence.Connections.Count);

            if (sequence.IsEmpty)
            {
                logger.LogInformation("Removing empty {Group}", groupKey);
                Sequences.TryRemove(groupKey, out _);
            }
            else
            {
                if (connection.OwnsSequence)
                {
                    // Re-assign ownership when the owner leaves
                    var newOwner = SelectNewOwner(sequence);
                    if (newOwner != null)
                    {
                        logger.LogInformation("Reassigning ownership to {NewCallsign} for {Group}",
                            newOwner.Callsign, groupKey);
                        await Clients.Client(newOwner.Id).SendAsync("OwnershipGranted", new OwnershipGrantedNotification(groupKey.AirportIdentifier));
                        newOwner.OwnsSequence = true;
                    }
                }
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

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var connection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var callsign = connection?.Callsign ?? "Unknown";

        if (sequenceUpdatedNotification.AirportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "{Callsign} attempted to update {Airport} but is part of {Group}",
                callsign, sequenceUpdatedNotification.AirportIdentifier, groupKey);
            return;
        }

        if (sequence is null)
        {
            logger.LogWarning(
                "{Callsign} attempted to update {Group} but no sequence exists",
                callsign,
                groupKey);
            return;
        }

        if (!connection.OwnsSequence)
        {
            logger.LogWarning("{Callsign} attempted to update {Group} but is not the owner",
                callsign,
                groupKey);
            return;
        }

        logger.LogDebug("{Callsign} updating {Group}",
            callsign, groupKey);

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
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "InsertFlight", envelope);
    }

    public async Task InsertOvershoot(InsertOvershootRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "InsertOvershoot", envelope);
    }

    public async Task InsertDeparture(InsertDepartureRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "InsertDeparture", envelope);
    }

    public async Task MoveFlight(MoveFlightRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "MoveFlight", envelope);
    }

    public async Task SwapFlights(SwapFlightsRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "SwapFlights", envelope);
    }

    public async Task Remove(RemoveRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "Remove", envelope);
    }

    public async Task Desequence(DesequenceRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "Desequence", envelope);
    }

    public async Task MakePending(MakePendingRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "MakePending", envelope);
    }

    public async Task MakeStable(MakeStableRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "MakeStable", envelope);
    }

    public async Task Recompute(RecomputeRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "Recompute", envelope);
    }

    public async Task ResumeSequencing(ResumeSequencingRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ResumeSequencing", envelope);
    }

    public async Task ZeroDelay(ZeroDelayRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ZeroDelay", envelope);
    }

    public async Task ChangeRunway(ChangeRunwayRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ChangeRunway", envelope);
    }

    public async Task ChangeRunwayMode(ChangeRunwayModeRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ChangeRunwayMode", envelope);
    }

    public async Task ChangeFeederFixEstimate(ChangeFeederFixEstimateRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ChangeFeederFixEstimate", envelope);
    }

    public async Task CreateSlot(CreateSlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "CreateSlot", envelope);
    }

    public async Task ModifySlot(ModifySlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "ModifySlot", envelope);
    }

    public async Task DeleteSlot(DeleteSlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return;

        await SendToFlowController(request.AirportIdentifier, "DeleteSlot", envelope);
    }

    private RequestEnvelope<T>? CreateRequestEnvelope<T>(T request, string airportIdentifier)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to create envelope but is not part of any group",
                Context.ConnectionId);
            return null;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var senderConnection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        if (senderConnection == null)
        {
            logger.LogWarning("Connection {ConnectionId} attempted to create envelope but connection not found",
                Context.ConnectionId);
            return null;
        }

        return RequestEnvelopeHelper.CreateEnvelope(request, senderConnection.Callsign, Context.ConnectionId, senderConnection.Role);
    }



    private async Task SendToFlowController<T>(string airportIdentifier, string messageType, T request)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MessageType} but is not part of any group",
                Context.ConnectionId, messageType);
            return;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var senderConnection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var senderCallsign = senderConnection?.Callsign ?? "Unknown";

        if (airportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "{Callsign} attempted to send {MessageType} for {Airport} but is part of {Group}",
                senderCallsign, messageType, airportIdentifier, groupKey);
            return;
        }

        if (sequence == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MessageType} to owner of {Group} but no sequence exists",
                senderCallsign, messageType, groupKey);
            return;
        }

        var flowController = sequence.Connections.SingleOrDefault(c => c.OwnsSequence);
        if (flowController == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MessageType} to owner of {Group} but no owner exists",
                senderCallsign, messageType, groupKey);
            return;
        }

        // Don't send messages to yourself
        if (flowController.Id == Context.ConnectionId)
        {
            logger.LogWarning("{Callsign} attempted to send {MessageType} to itself as owner of {Group}",
                senderCallsign, messageType, groupKey);
            return;
        }

        logger.LogDebug("{Callsign} sending {MessageType} to {FlowControllerCallsign} for {Group}",
            senderCallsign, messageType, flowController.Callsign, groupKey);

        await Clients.Client(flowController.Id).SendAsync(messageType, request);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        // Try to get the callsign before we remove the connection
        string callsign = "Unknown";
        if (Connections.TryGetValue(connectionId, out var groupKey))
        {
            var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
            var connection = sequence?.Connections.SingleOrDefault(c => c.Id == connectionId);
            callsign = connection?.Callsign ?? "Unknown";
        }

        if (exception is not null)
        {
            logger.LogError(exception, "{Callsign} disconnected with error", callsign);
        }
        else
        {
            logger.LogInformation("{Callsign} disconnected", callsign);
        }

        if (Connections.TryGetValue(connectionId, out groupKey))
        {
            var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
            if (sequence != null)
            {
                // Check if the disconnecting connection was the owner or flow controller before removing it
                var disconnectingConnection = sequence.Connections.FirstOrDefault(c => c.Id == connectionId);
                var wasOwner = disconnectingConnection?.OwnsSequence ?? false;
                var wasFlowController = disconnectingConnection?.Role == Role.Flow;

                await Groups.RemoveFromGroupAsync(connectionId, groupKey.Value);
                sequence.RemoveConnection(connectionId);

                logger.LogInformation("{Callsign} left {Group}. Remaining connections: {Count}",
                    callsign, groupKey, sequence.Connections.Count);

                if (sequence.IsEmpty)
                {
                    logger.LogInformation("Removing empty {Group}", groupKey);
                    Sequences.TryRemove(groupKey, out _);
                }
                else
                {
                    if (wasOwner)
                    {
                        // Re-assign ownership when the owner disconnects
                        var newOwner = SelectNewOwner(sequence);
                        if (newOwner != null)
                        {
                            logger.LogInformation("Reassigning ownership to {NewCallsign} for {Group} after disconnect",
                                newOwner.Callsign, groupKey);
                            await Clients.Client(newOwner.Id).SendAsync("OwnershipGranted", new OwnershipGrantedNotification(groupKey.AirportIdentifier));
                            newOwner.OwnsSequence = true;
                        }
                    }

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

    private static Connection? SelectNewOwner(Sequence sequence)
    {
        // Prioritize flow controllers first
        var flowController = sequence.Connections.FirstOrDefault(c =>
            !c.OwnsSequence && c.Role == Role.Flow);

        if (flowController != null)
            return flowController;

        // Fall back to any other available connection
        return sequence.Connections.FirstOrDefault(c => !c.OwnsSequence);
    }
}
