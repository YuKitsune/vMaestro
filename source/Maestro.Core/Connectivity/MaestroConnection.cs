using Maestro.Core.Configuration;
using Maestro.Core.Connectivity.Contracts;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace Maestro.Core.Connectivity;

public class MaestroConnection : IMaestroConnection, IAsyncDisposable
{
    readonly List<PeerInfo> _peers = new();
    readonly CancellationTokenSource _rootCancellationTokenSource = new();
    readonly ServerConfiguration _serverConfiguration;
    readonly string _airportIdentifier;
    readonly IMediator _mediator;
    readonly ILogger _logger;

    HubConnection? _hubConnection;

    public string Partition { get; }

    // [MemberNotNull(nameof(_serverConfiguration))]
    public bool IsConnected => _hubConnection is not null && _hubConnection.State is not HubConnectionState.Disconnected;
    public Role Role { get; private set; }
    public bool IsMaster { get; private set; }
    public IReadOnlyList<PeerInfo> Peers => _peers;

    public MaestroConnection(
        ServerConfiguration serverConfiguration,
        string airportIdentifier,
        string partition,
        IMediator mediator,
        ILogger logger)
    {
        _serverConfiguration = serverConfiguration;
        _airportIdentifier = airportIdentifier;
        Partition = partition;

        _mediator = mediator;
        _logger = logger;
    }

    public async Task Start(string callsign, CancellationToken cancellationToken)
    {
        if (IsConnected)
            throw new MaestroException("Cannot start the connection when already connected.");

        Role = RoleHelper.GetRoleFromCallsign(callsign);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_serverConfiguration.Uri + $"?partition={Partition}&airportIdentifier={_airportIdentifier}&callsign={callsign}&role={Role}")
            .WithServerTimeout(TimeSpan.FromSeconds(_serverConfiguration.TimeoutSeconds))
            .WithAutomaticReconnect()
            .WithStatefulReconnect()
            .AddNewtonsoftJsonProtocol(x =>
            {
                x.PayloadSerializerSettings.ContractResolver = new DefaultContractResolver();
                x.PayloadSerializerSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;
            })
            .Build();

        SubscribeToNotifications(_hubConnection);
        SubscribeToConnectionEvents(_hubConnection);

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
            await _mediator.Publish(new ConnectionStartedNotification(_airportIdentifier), cancellationToken);
        }
        catch (Exception exception)
        {
            throw new MaestroException($"Failed to connect to sequence: {exception.Message}", exception);
        }
    }

    public async Task Stop(CancellationToken cancellationToken)
    {
        if (!IsConnected)
            throw new MaestroException("Cannot stop the connection when not connected.");

        try
        {
            // IsConnected ensures _hubConnection is not null
            await _hubConnection!.StopAsync(cancellationToken);
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            IsMaster = true;
            Role = Role.Flow;

            await _mediator.Publish(new ConnectionStoppedNotification(_airportIdentifier), cancellationToken);
        }
        catch (Exception exception)
        {
            throw new MaestroException($"Failed to leave sequence: {exception.Message}", exception);
        }
    }

    public async Task Invoke<T>(T message, CancellationToken cancellationToken)
        where T : class, IRequest
    {
        if (!IsConnected)
            throw new MaestroException("Cannot invoke a method when not connected.");

        var methodName = GetMethodName(message);

        // IsConnected ensures _hubConnection is not null
        var response = await _hubConnection!.InvokeAsync<RelayResponse>(methodName, message, cancellationToken);
        if (!response.Success)
        {
            await _mediator.Publish(new ErrorNotification(new MaestroException(response.ErrorMessage)), cancellationToken);
        }
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken)
        where T : class, INotification
    {
        if (!IsConnected)
            throw new MaestroException("Cannot send a message when not connected.");

        var methodName = GetMethodName(message);

        // IsConnected ensures _hubConnection is not null
        await _hubConnection!.SendAsync(methodName, message, cancellationToken);
    }

    private static string GetMethodName(object request)
    {
        // Map request type names to hub method names (removed "Request" suffix)
        return request switch
        {
            // Notifications
            SessionUpdatedNotification => "SessionUpdated",
            FlightUpdatedNotification => "FlightUpdated",
            CoordinationNotification => "Coordination",

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
            ManualDelayRequest => "ManualDelay",
            CreateSlotRequest => "CreateSlot",
            ModifySlotRequest => "ModifySlot",
            DeleteSlotRequest => "DeleteSlot",
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unsupported request type: " + request.GetType().Name)
        };
    }

    void SubscribeToNotifications(HubConnection hubConnection)
    {
        hubConnection.On<ConnectionInitializedNotification>("ConnectionInitialized", async notification =>
        {
            if (notification.AirportIdentifier != _airportIdentifier)
                return;

            IsMaster = notification.IsMaster;

            _peers.AddRange(notification.ConnectedPeers);

            // TODO: Clear the session if it's null
            if (notification.Session is not null)
            {
                await _mediator.Send(
                    new RestoreSessionRequest(notification.AirportIdentifier, notification.Session),
                    GetMessageCancellationToken());
            }
        });

        hubConnection.On<OwnershipGrantedNotification>("OwnershipGranted", request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            IsMaster = true;
            _logger.Information("Ownership granted for {AirportIdentifier}", _airportIdentifier);
        });

        hubConnection.On<OwnershipRevokedNotification>("OwnershipRevoked", request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            IsMaster = false;
            _logger.Information("Ownership revoked for {AirportIdentifier}", _airportIdentifier);
        });

        hubConnection.On<SessionUpdatedNotification>("SessionUpdated", async sequenceUpdatedNotification =>
        {
            if (sequenceUpdatedNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(
                new RestoreSessionRequest(
                    sequenceUpdatedNotification.AirportIdentifier,
                    sequenceUpdatedNotification.Session),
                GetMessageCancellationToken());
        });

        hubConnection.On<FlightUpdatedNotification>("FlightUpdated", async flightUpdatedNotification =>
        {
            if (flightUpdatedNotification.Destination != _airportIdentifier)
                return;

            await _mediator.Publish(flightUpdatedNotification, GetMessageCancellationToken());
        });

        hubConnection.On<CoordinationNotification>("Coordination", async coordinationNotification =>
        {
            if (coordinationNotification.AirportIdentifier != _airportIdentifier)
                return;

            _logger.Information("Received coordination message {Message}", coordinationNotification.Message);
            await _mediator.Publish(coordinationNotification, GetMessageCancellationToken());
        });

        hubConnection.On<PeerConnectedNotification>("PeerConnected", async clientConnectedNotification =>
        {
            if (clientConnectedNotification.AirportIdentifier != _airportIdentifier)
                return;

            _peers.Add(new PeerInfo(clientConnectedNotification.Callsign, clientConnectedNotification.Role));
            await _mediator.Publish(clientConnectedNotification, GetMessageCancellationToken());
        });

        hubConnection.On<PeerDisconnectedNotification>("PeerDisconnected", async clientDisconnectedNotification =>
        {
            if (clientDisconnectedNotification.AirportIdentifier != _airportIdentifier)
                return;

            _peers.RemoveAll(p => p.Callsign == clientDisconnectedNotification.Callsign);
            await _mediator.Publish(clientDisconnectedNotification, GetMessageCancellationToken());
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("InsertFlight", async envelope =>
        {
            var request = (InsertFlightRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.InsertDummy);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("InsertDeparture", async envelope =>
        {
            var request = (InsertDepartureRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.InsertDeparture);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("MoveFlight", async envelope =>
        {
            var request = (MoveFlightRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MoveFlight);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("SwapFlights", async envelope =>
        {
            var request = (SwapFlightsRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MoveFlight);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("Remove", async envelope =>
        {
            var request = (RemoveRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.RemoveFlight);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("Desequence", async envelope =>
        {
            var request = (DesequenceRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Desequence);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("MakePending", async envelope =>
        {
            var request = (MakePendingRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MakePending);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("MakeStable", async envelope =>
        {
            var request = (MakeStableRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.MakeStable);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("Recompute", async envelope =>
        {
            var request = (RecomputeRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Recompute);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ResumeSequencing", async envelope =>
        {
            var request = (ResumeSequencingRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.Resequence);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ManualDelay", async envelope =>
        {
            var request = (ManualDelayRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManualDelay);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ChangeRunway", async envelope =>
        {
            var request = (ChangeRunwayRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeRunway);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ChangeRunwayMode", async envelope =>
        {
            var request = (ChangeRunwayModeRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeTerminalConfiguration);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ChangeFeederFixEstimate", async envelope =>
        {
            var request = (ChangeFeederFixEstimateRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ChangeFeederFixEstimate);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("CreateSlot", async envelope =>
        {
            var request = (CreateSlotRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("ModifySlot", async envelope =>
        {
            var request = (ModifySlotRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });

        hubConnection.On<RequestEnvelope, RelayResponse>("DeleteSlot", async envelope =>
        {
            var request = (DeleteSlotRequest) envelope.Request;
            if (request.AirportIdentifier != _airportIdentifier)
                return RelayResponse.CreateFailure("Airport identifier mismatch");

            return await ProcessEnvelopedRequest(envelope, ActionKeys.ManageSlots);
        });
    }

    void SubscribeToConnectionEvents(HubConnection hubConnection)
    {
        hubConnection.Closed += async (exception) =>
        {
            await _mediator.Publish(new ConnectionStoppedNotification(_airportIdentifier), CancellationToken.None);

            // If the connection was closed due to an error, take ownership of the sequence and report the error
            if (exception != null && !_rootCancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error(exception, "Connection for {AirportIdentifier} lost", _airportIdentifier);
                await _mediator.Publish(new ErrorNotification(exception), CancellationToken.None);
            }
            else
            {
                _logger.Information("Connection for {AirportIdentifier} closed", _airportIdentifier);
            }

            IsMaster = true;
            await _mediator.Publish(new ConnectionStoppedNotification(_airportIdentifier), CancellationToken.None);
        };

        hubConnection.Reconnecting += async exception =>
        {
            _logger.Warning(exception, "Connection for {AirportIdentifier} reconnecting", _airportIdentifier);
            await _mediator.Publish(new ReconnectingNotification(_airportIdentifier), CancellationToken.None);
        };

        hubConnection.Reconnected += async connectionId =>
        {
            _logger.Information(
                "Connection for {AirportIdentifier} reconnected with ConnectionId {ConnectionId}",
                _airportIdentifier, connectionId);
            await _mediator.Publish(new ReconnectedNotification(_airportIdentifier), CancellationToken.None);
        };
    }

    CancellationToken GetMessageCancellationToken()
    {
        var source = new CancellationTokenSource();
        source.CancelAfter(TimeSpan.FromSeconds(5));

        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(_rootCancellationTokenSource.Token, source.Token);
        return linkedSource.Token;
    }

    async Task<RelayResponse> ProcessEnvelopedRequest(RequestEnvelope envelope, string actionKey)
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
        if (_hubConnection is not null)
            await _hubConnection.DisposeAsync();

        _hubConnection = null;
        _rootCancellationTokenSource.Cancel();
        _rootCancellationTokenSource.Dispose();
    }
}
