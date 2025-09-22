using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog;

namespace Maestro.Core.Infrastructure;

// TODO:
// - [X] Dispatch messages to both Mediator (for in-memory stuff) and SignalR (when connected); Disregard permissions for now
// - [X] Invoke SignalR methods when initializing the connection, ensure it's all synchronous to prevent dirty states
// - [X] Encapsulate the in-memory sequence, the SignalR connection, and the UI stuff into a Session object
// - [X] Test with two clients connected at once, what breaks?
// - [X] Restrict access to certain functions from certain clients
// - [X] Show information messages when things happen
// - [ ] Coordination messages

public class MaestroConnection : IAsyncDisposable
{
    readonly ServerConfiguration _serverConfiguration;
    readonly CancellationTokenSource _rootCancellationTokenSource = new();
    readonly string _partition;
    readonly string _airportIdentifier;
    readonly HubConnection _hubConnection;
    readonly IMediator _mediator;
    readonly ILogger _logger;

    string? _currentPosition;
    Role? _currentRole;

    public bool IsConnected => _hubConnection.State is not HubConnectionState.Disconnected;
    public string Partition => _partition;

    public MaestroConnection(
        string partition,
        string airportIdentifier,
        ServerConfiguration serverConfiguration,
        HubConnection hubConnection,
        IMediator mediator,
        ILogger logger)
    {
        _partition = partition;
        _airportIdentifier = airportIdentifier;
        _serverConfiguration = serverConfiguration;
        _hubConnection = hubConnection;
        _mediator = mediator;
        _logger = logger;
        AirportIdentifier = airportIdentifier;
        SubscribeToNotifications();
        SubscribeToConnectionEvents();
    }

    public string AirportIdentifier { get; }

    // TODO: Find a better place for this
    public record SequenceStartResult(
        bool OwnsSequence,
        SequenceMessage? Sequence,
        Role Role,
        IReadOnlyList<PeerInfo> ConnectedClients);

    public async Task<SequenceStartResult> Start(string position, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            if (_hubConnection.State == HubConnectionState.Disconnected)
                await _hubConnection.StartAsync(timeoutCts.Token);
        }
        catch (Exception exception)
        {
            throw new MaestroException("Failed to connect to server", exception);
        }

        try
        {
            var role = PermissionHelper.GetRoleFromCallsign(position);
            var response = await _hubConnection.InvokeAsync<JoinSequenceResponse>(
                "JoinSequence",
                new JoinSequenceRequest(_partition, AirportIdentifier, position, role),
                cancellationToken);

            // Store position and role for potential reconnection
            _currentPosition = position;
            _currentRole = role;

            // Flow controllers now handle permissions client-side

            await _mediator.Publish(new SessionConnectedNotification(_airportIdentifier, role, response.ConnectedPeers), cancellationToken);
            return new SequenceStartResult(response.OwnsSequence, response.Sequence, role, response.ConnectedPeers);
        }
        catch (Exception exception)
        {
            throw new MaestroException("Failed to connect to sequence", exception);
        }
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        try
        {
            await _hubConnection.StopAsync(cancellationToken);
            await _mediator.Publish(new SessionDisconnectedNotification(_airportIdentifier), cancellationToken);
        }
        catch (Exception exception)
        {
            throw new MaestroException("Failed to leave sequence", exception);
        }
    }

    public async Task Invoke<T>(T message, CancellationToken cancellationToken)
        where T : class, IRequest
    {
        var methodName = GetMethodName(message);
        var response = await _hubConnection.InvokeAsync<RelayResponse>(methodName, message, cancellationToken);
        if (!response.Success)
        {
            await _mediator.Publish(new ErrorNotification(new MaestroException(response.ErrorMessage)), cancellationToken);
        }
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken)
        where T : class, INotification
    {
        var methodName = GetMethodName(message);
        await _hubConnection.SendAsync(methodName, message, cancellationToken);
    }

    private static string GetMethodName(object request)
    {
        // Map request type names to hub method names (removed "Request" suffix)
        return request switch
        {
            // Notifications
            SequenceUpdatedNotification => "SequenceUpdated",
            FlightUpdatedNotification => "FlightUpdated",
            InformationNotification => "Information",

            // Requests
            ChangeRunwayRequest => "ChangeRunway",
            ChangeRunwayModeRequest => "ChangeRunwayMode",
            ChangeFeederFixEstimateRequest => "ChangeFeederFixEstimate",
            InsertFlightRequest => "InsertFlight",
            InsertDepartureRequest => "InsertDeparture",
            InsertOvershootRequest => "InsertOvershoot",
            MoveFlightRequest => "MoveFlight",
            SwapFlightsRequest => "SwapFlights",
            RemoveRequest => "Remove",
            DesequenceRequest => "Desequence",
            MakePendingRequest => "MakePending",
            MakeStableRequest => "MakeStable",
            RecomputeRequest => "Recompute",
            ResumeSequencingRequest => "ResumeSequencing",
            ZeroDelayRequest => "ZeroDelay",
            CreateSlotRequest => "CreateSlot",
            ModifySlotRequest => "ModifySlot",
            DeleteSlotRequest => "DeleteSlot",
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unsupported request type: " + request.GetType().Name)
        };
    }

    void SubscribeToNotifications()
    {
        _hubConnection.On<OwnershipGrantedNotification>("OwnershipGranted", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(request, GetMessageCancellationToken());
        });

        _hubConnection.On<OwnershipRevokedNotification>("OwnershipRevoked", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(request, GetMessageCancellationToken());
        });

        _hubConnection.On<SequenceUpdatedNotification>("SequenceUpdated", async sequenceUpdatedNotification =>
        {
            if (sequenceUpdatedNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(sequenceUpdatedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<FlightUpdatedNotification>("FlightUpdated", async flightUpdatedNotification =>
        {
            if (flightUpdatedNotification.Destination != _airportIdentifier)
                return;

            await _mediator.Publish(flightUpdatedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<InformationNotification>("Information", async infoNotification =>
        {
            if (infoNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(infoNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<PeerConnectedNotification>("PeerConnected", async clientConnectedNotification =>
        {
            if (clientConnectedNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(clientConnectedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<PeerDisconnectedNotification>("PeerDisconnected", async clientDisconnectedNotification =>
        {
            if (clientDisconnectedNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(clientDisconnectedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<RequestEnvelope<InsertFlightRequest>, RelayResponse>("InsertFlight", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.InsertDummy);
        });

        _hubConnection.On<RequestEnvelope<InsertDepartureRequest>, RelayResponse>("InsertDeparture", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.InsertDeparture);
        });

        _hubConnection.On<RequestEnvelope<MoveFlightRequest>, RelayResponse>("MoveFlight", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MoveFlight);
        });

        _hubConnection.On<RequestEnvelope<SwapFlightsRequest>, RelayResponse>("SwapFlights", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MoveFlight);
        });

        _hubConnection.On<RequestEnvelope<RemoveRequest>, RelayResponse>("Remove", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.RemoveFlight);
        });

        _hubConnection.On<RequestEnvelope<DesequenceRequest>, RelayResponse>("Desequence", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Desequence);
        });

        _hubConnection.On<RequestEnvelope<MakePendingRequest>, RelayResponse>("MakePending", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MakePending);
        });

        _hubConnection.On<RequestEnvelope<MakeStableRequest>, RelayResponse>("MakeStable", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MakeStable);
        });

        _hubConnection.On<RequestEnvelope<RecomputeRequest>, RelayResponse>("Recompute", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Recompute);
        });

        _hubConnection.On<RequestEnvelope<ResumeSequencingRequest>, RelayResponse>("ResumeSequencing", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Resequence);
        });

        _hubConnection.On<RequestEnvelope<ZeroDelayRequest>, RelayResponse>("ZeroDelay", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManualDelay);
        });

        _hubConnection.On<RequestEnvelope<ChangeRunwayRequest>, RelayResponse>("ChangeRunway", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeRunway);
        });

        _hubConnection.On<RequestEnvelope<ChangeRunwayModeRequest>, RelayResponse>("ChangeRunwayMode", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeTerminalConfiguration);
        });

        _hubConnection.On<RequestEnvelope<ChangeFeederFixEstimateRequest>, RelayResponse>("ChangeFeederFixEstimate", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeFeederFixEstimate);
        });

        _hubConnection.On<RequestEnvelope<CreateSlotRequest>, RelayResponse>("CreateSlot", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });

        _hubConnection.On<RequestEnvelope<ModifySlotRequest>, RelayResponse>("ModifySlot", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });

        _hubConnection.On<RequestEnvelope<DeleteSlotRequest>, RelayResponse>("DeleteSlot", async envelope =>
        {
            if (envelope.Request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });
    }

    void SubscribeToConnectionEvents()
    {
        _hubConnection.Closed += async (exception) =>
        {
            await _mediator.Publish(new SessionDisconnectedNotification(_airportIdentifier), CancellationToken.None);

            // If the connection was closed due to an error, take ownership of the sequence and report the error
            if (exception != null && !_rootCancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error(exception, "Connection for {AirportIdentifier} lost", _airportIdentifier);
                await _mediator.Publish(new OwnershipGrantedNotification(_airportIdentifier, _currentRole.Value), CancellationToken.None);
                await _mediator.Publish(new ErrorNotification(exception), CancellationToken.None);
            }
            else
            {
                _logger.Information("Connection for {AirportIdentifier} closed", _airportIdentifier);
            }
        };

        _hubConnection.Reconnecting += exception =>
        {
            _logger.Warning(exception, "Connection for {AirportIdentifier} reconnecting", _airportIdentifier);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += async connectionId =>
        {
            _logger.Information(
                "Connection for {AirportIdentifier} reconnected with connectionId {ConnectionId}",
                _airportIdentifier, connectionId);

            // Attempt to rejoin the sequence if we have stored position info
            if (_currentPosition != null && _currentRole != null)
            {
                try
                {
                    _logger.Information("Attempting to rejoin sequence for position {Position}", _currentPosition);

                    var response = await _hubConnection.InvokeAsync<JoinSequenceResponse>(
                        "JoinSequence",
                        new JoinSequenceRequest(_partition, AirportIdentifier, _currentPosition, _currentRole.Value),
                        CancellationToken.None);

                    _logger.Information("Successfully rejoined sequence for {Position}. OwnsSequence: {OwnsSequence}",
                        _currentPosition, response.OwnsSequence);

                    await _mediator.Publish(new SessionConnectedNotification(_airportIdentifier, _currentRole.Value, response.ConnectedPeers), CancellationToken.None);

                    // If we don't own the sequence after reconnecting, revoke local ownership
                    if (!response.OwnsSequence)
                    {
                        await _mediator.Publish(new OwnershipRevokedNotification(_airportIdentifier), CancellationToken.None);
                    }

                    await _mediator.Publish(new InformationNotification(_airportIdentifier, DateTimeOffset.UtcNow, "Connection to server restored", LocalOnly: true), CancellationToken.None);
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, "Failed to rejoin sequence for position {Position} after reconnection", _currentPosition);
                    // Keep local ownership since we couldn't rejoin
                    await _mediator.Publish(new OwnershipGrantedNotification(_airportIdentifier, _currentRole.Value), CancellationToken.None);
                    await _mediator.Publish(new ErrorNotification(exception), CancellationToken.None);
                }
            }
        };
    }

    CancellationToken GetMessageCancellationToken()
    {
        var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromSeconds(5));

        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellationTokenSource.Token, source.Token);
        return linkedSource.Token;
    }

    async Task<RelayResponse> ProcessEnvelopedRequest<T>(RequestEnvelope<T> envelope, string actionKey) where T : class, IRequest
    {
        var cancellationToken = GetMessageCancellationToken();

        var response = await _mediator.Send(
            new RelayRequest
            {
                Envelope = new RequestEnvelope
                {
                    OriginatingCallsign = envelope.OriginatingCallsign,
                    OriginatingConnectionId = envelope.OriginatingConnectionId,
                    OriginatingRole = envelope.OriginatingRole,
                    Request = envelope.Request
                },
                ActionKey = actionKey
            },
            cancellationToken);

        if (!response.Success)
        {
            _logger.Warning("Request {ActionKey} from {Callsign} failed: {ErrorMessage}",
                actionKey, envelope.OriginatingCallsign, response.ErrorMessage);
        }
        else
        {
            _logger.Information("Request {ActionKey} from {Callsign} processed successfully",
                actionKey, envelope.OriginatingCallsign);
        }

        return response;
    }

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
        _rootCancellationTokenSource.Cancel();
        _rootCancellationTokenSource.Dispose();
    }
}
