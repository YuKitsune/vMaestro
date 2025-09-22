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

        // Check if sequence exists and prevent observers from creating new sequences
        var sequenceExists = Sequences.ContainsKey(groupKey);
        if (!sequenceExists && request.Role == Role.Observer)
        {
            logger.LogInformation("{Callsign} attempted to create new sequence {Group}", request.Position, groupKey);
            throw new InvalidOperationException($"No active sequence found for {groupKey.AirportIdentifier}");
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
        else if (request.Role == Role.Observer)
        {
            // Observers cannot own sequences when connected to server
            connection.OwnsSequence = false;
        }
        else if (wasEmpty)
        {
            // First non-observer connection becomes flow controller if no FMP controller is present
            logger.LogInformation("Assigning {Callsign} as owner for {Group} (first non-observer connection)",
                request.Position, groupKey);
            connection.OwnsSequence = true;
        }

        // Get list of connected clients (excluding the one that just joined)
        var connectedClients = sequence.Connections
            .Where(c => c.Id != Context.ConnectionId)
            .Select(c => new PeerInfo(c.Callsign, c.Role))
            .ToList();

        // Broadcast to other clients that this client has connected
        await Clients.GroupExcept(groupKey.Value, Context.ConnectionId)
            .SendAsync("PeerConnected", new PeerConnectedNotification(groupKey.AirportIdentifier, request.Position, request.Role));

        return new JoinSequenceResponse(Context.ConnectionId, connection.OwnsSequence, sequence.LatestSequence, connectedClients);
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

            // Broadcast to remaining clients that this client has disconnected
            await Clients.Group(groupKey.Value)
                .SendAsync("PeerDisconnected", new PeerDisconnectedNotification(groupKey.AirportIdentifier, callsign));

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
                        await Clients.Client(newOwner.Id).SendAsync("OwnershipGranted", new OwnershipGrantedNotification(groupKey.AirportIdentifier, newOwner.Role));
                        newOwner.OwnsSequence = true;
                    }
                }

                // Check if only observers remain and disconnect them
                await DisconnectObserversIfOnlyObserversRemain(sequence, groupKey);
            }
        }

        Connections.Remove(Context.ConnectionId);
    }

    public async Task SequenceUpdated(SequenceUpdatedNotification sequenceUpdatedNotification)
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
            .SendAsync("SequenceUpdated", sequenceUpdatedNotification);
    }

    public async Task FlightUpdated(FlightUpdatedNotification flightUpdatedNotification)
    {
        await SendNotificationToFlowController(flightUpdatedNotification.Destination, "FlightUpdated", flightUpdatedNotification);
    }

    public async Task Information(InformationNotification informationNotification)
    {
        // Look up the group key for this connection
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to notify sequence but is not part of any group", Context.ConnectionId);
            return;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var connection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var callsign = connection?.Callsign ?? "Unknown";

        if (informationNotification.AirportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "{Callsign} attempted to notify {Airport} but is part of {Group}",
                callsign, informationNotification.AirportIdentifier, groupKey);
            return;
        }

        if (sequence is null)
        {
            logger.LogWarning(
                "{Callsign} attempted to notify {Group} but no sequence exists",
                callsign,
                groupKey);
            return;
        }

        if (!connection.OwnsSequence)
        {
            logger.LogWarning("{Callsign} attempted to notify {Group} but is not the owner",
                callsign,
                groupKey);
            return;
        }

        logger.LogDebug("{Callsign} notifying {Group}",
            callsign, groupKey);

        // Send to all clients in the group except the sender
        await Clients.GroupExcept(groupKey.Value, Context.ConnectionId)
            .SendAsync("Information", informationNotification);
    }

    public async Task<RelayResponse> InsertFlight(InsertFlightRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "InsertFlight", envelope);
    }

    public async Task<RelayResponse> InsertOvershoot(InsertOvershootRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "InsertOvershoot", envelope);
    }

    public async Task<RelayResponse> InsertDeparture(InsertDepartureRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "InsertDeparture", envelope);
    }

    public async Task<RelayResponse> MoveFlight(MoveFlightRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "MoveFlight", envelope);
    }

    public async Task<RelayResponse> SwapFlights(SwapFlightsRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "SwapFlights", envelope);
    }

    public async Task<RelayResponse> Remove(RemoveRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "Remove", envelope);
    }

    public async Task<RelayResponse> Desequence(DesequenceRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "Desequence", envelope);
    }

    public async Task<RelayResponse> MakePending(MakePendingRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "MakePending", envelope);
    }

    public async Task<RelayResponse> MakeStable(MakeStableRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "MakeStable", envelope);
    }

    public async Task<RelayResponse> Recompute(RecomputeRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "Recompute", envelope);
    }

    public async Task<RelayResponse> ResumeSequencing(ResumeSequencingRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ResumeSequencing", envelope);
    }

    public async Task<RelayResponse> ZeroDelay(ZeroDelayRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ZeroDelay", envelope);
    }

    public async Task<RelayResponse> ChangeRunway(ChangeRunwayRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ChangeRunway", envelope);
    }

    public async Task<RelayResponse> ChangeRunwayMode(ChangeRunwayModeRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ChangeRunwayMode", envelope);
    }

    public async Task<RelayResponse> ChangeFeederFixEstimate(ChangeFeederFixEstimateRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ChangeFeederFixEstimate", envelope);
    }

    public async Task<RelayResponse> CreateSlot(CreateSlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "CreateSlot", envelope);
    }

    public async Task<RelayResponse> ModifySlot(ModifySlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "ModifySlot", envelope);
    }

    public async Task<RelayResponse> DeleteSlot(DeleteSlotRequest request)
    {
        var envelope = CreateRequestEnvelope(request, request.AirportIdentifier);
        if (envelope == null)
            return RelayResponse.CreateFailure("Failed to create request envelope");

        return await SendToFlowController(request.AirportIdentifier, "DeleteSlot", envelope);
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

    async Task<RelayResponse> SendToFlowController<T>(string airportIdentifier, string methodName, T request)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MethodName} but is not part of any group",
                Context.ConnectionId, methodName);
            return RelayResponse.CreateFailure("Connection not part of any group");
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var senderConnection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var senderCallsign = senderConnection?.Callsign ?? "Unknown";

        if (airportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "{Callsign} attempted to send {MethodName} for {Airport} but is part of {Group}",
                senderCallsign, methodName, airportIdentifier, groupKey);
            return RelayResponse.CreateFailure($"Airport mismatch: expected {groupKey.AirportIdentifier}, got {airportIdentifier}");
        }

        if (sequence == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to owner of {Group} but no sequence exists",
                senderCallsign, methodName, groupKey);
            return RelayResponse.CreateFailure("No sequence exists");
        }

        var flowController = sequence.Connections.SingleOrDefault(c => c.OwnsSequence);
        if (flowController == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to owner of {Group} but no owner exists",
                senderCallsign, methodName, groupKey);
            return RelayResponse.CreateFailure("No flow controller available");
        }

        // Don't send messages to yourself
        if (flowController.Id == Context.ConnectionId)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to itself as owner of {Group}",
                senderCallsign, methodName, groupKey);
            return RelayResponse.CreateFailure("Cannot send request to self");
        }

        logger.LogDebug("{Callsign} sending {MethodName} to {FlowControllerCallsign} for {Group}",
            senderCallsign, methodName, flowController.Callsign, groupKey);

        try
        {
            var response = await Clients.Client(flowController.Id).InvokeAsync<RelayResponse>(methodName, request, CancellationToken.None);

            if (!response.Success)
            {
                logger.LogWarning("{Callsign} received error response from {FlowControllerCallsign} for {MethodName} in {Group}: {ErrorMessage}",
                    senderCallsign, flowController.Callsign, methodName, groupKey, response.ErrorMessage);
            }

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send {MethodName} from {Callsign} to {FlowControllerCallsign}",
                methodName, senderCallsign, flowController.Callsign);
            return RelayResponse.CreateFailure($"Communication error: {ex.Message}");
        }
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

                // Broadcast to remaining clients that this client has disconnected
                await Clients.Group(groupKey.Value)
                    .SendAsync("PeerDisconnected", new PeerDisconnectedNotification(groupKey.AirportIdentifier, callsign));

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
                            await Clients.Client(newOwner.Id).SendAsync("OwnershipGranted", new OwnershipGrantedNotification(groupKey.AirportIdentifier, newOwner.Role));
                            newOwner.OwnsSequence = true;
                        }
                    }

                    // Check if only observers remain and disconnect them
                    await DisconnectObserversIfOnlyObserversRemain(sequence, groupKey);
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

    async Task SendNotificationToFlowController<T>(string airportIdentifier, string methodName, T notification)
    {
        if (!Connections.TryGetValue(Context.ConnectionId, out var groupKey))
        {
            logger.LogWarning("Connection {ConnectionId} attempted to send {MethodName} but is not part of any group",
                Context.ConnectionId, methodName);
            return;
        }

        var sequence = Sequences.TryGetValue(groupKey, out var seq) ? seq : null;
        var senderConnection = sequence?.Connections.SingleOrDefault(c => c.Id == Context.ConnectionId);
        var senderCallsign = senderConnection?.Callsign ?? "Unknown";

        if (airportIdentifier != groupKey.AirportIdentifier)
        {
            logger.LogWarning(
                "{Callsign} attempted to send {MethodName} for {Airport} but is part of {Group}",
                senderCallsign, methodName, airportIdentifier, groupKey);
            return;
        }

        if (sequence == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to owner of {Group} but no sequence exists",
                senderCallsign, methodName, groupKey);
            return;
        }

        var flowController = sequence.Connections.SingleOrDefault(c => c.OwnsSequence);
        if (flowController == null)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to owner of {Group} but no owner exists",
                senderCallsign, methodName, groupKey);
            return;
        }

        // Don't send messages to yourself
        if (flowController.Id == Context.ConnectionId)
        {
            logger.LogWarning("{Callsign} attempted to send {MethodName} to itself as owner of {Group}",
                senderCallsign, methodName, groupKey);
            return;
        }

        logger.LogDebug("{Callsign} sending {MethodName} to {FlowControllerCallsign} for {Group}",
            senderCallsign, methodName, flowController.Callsign, groupKey);

        await Clients.Client(flowController.Id).SendAsync(methodName, notification);
    }

    private static Connection? SelectNewOwner(Sequence sequence)
    {
        // Prioritize flow controllers first
        var flowController = sequence.Connections.FirstOrDefault(c =>
            !c.OwnsSequence && c.Role == Role.Flow);

        if (flowController != null)
            return flowController;

        // Fall back to any other available connection, exclude observers
        return sequence.Connections.FirstOrDefault(c => !c.OwnsSequence && c.Role != Role.Observer);
    }

    private async Task DisconnectObserversIfOnlyObserversRemain(Sequence sequence, GroupKey groupKey)
    {
        // Check if only observers remain
        var nonObservers = sequence.Connections.Where(c => c.Role != Role.Observer).ToList();
        var observers = sequence.Connections.Where(c => c.Role == Role.Observer).ToList();

        if (nonObservers.Count == 0 && observers.Count > 0)
        {
            logger.LogInformation("Only observers remain in {Group}. Disconnecting {Count} observers to allow group cleanup",
                groupKey, observers.Count);

            // Disconnect all remaining observers
            var disconnectTasks = observers.Select(async observer =>
            {
                try
                {
                    // Notify the observer to revert to offline mode and force disconnect
                    await Clients.Client(observer.Id).SendAsync("OwnershipGranted",
                        new OwnershipGrantedNotification(groupKey.AirportIdentifier, observer.Role));

                    // Send force disconnect message to the observer client
                    await Clients.Client(observer.Id).SendAsync("ForceDisconnect",
                        "All controllers have disconnected from Maestro Server. Reverting to offline mode.");

                    // Remove from group and connections tracking
                    await Groups.RemoveFromGroupAsync(observer.Id, groupKey.Value);
                    Connections.Remove(observer.Id);

                    logger.LogInformation("Sent disconnect signal to observer {Callsign} from {Group} - reverting to offline mode",
                        observer.Callsign, groupKey);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to disconnect observer {Callsign} from {Group}",
                        observer.Callsign, groupKey);
                }
            });

            await Task.WhenAll(disconnectTasks);

            // Remove all observer connections from the sequence
            foreach (var observer in observers)
            {
                sequence.RemoveConnection(observer.Id);
            }

            // The sequence should now be empty and will be removed
            if (sequence.IsEmpty)
            {
                logger.LogInformation("Removing empty {Group} after disconnecting observers", groupKey);
                Sequences.TryRemove(groupKey, out _);
            }
        }
    }
}
