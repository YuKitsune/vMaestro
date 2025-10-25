using Maestro.Core.Configuration;
using Maestro.Core.Handlers;
using Maestro.Core.Messages;
using Maestro.Core.Messages.Connectivity;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Serialization;
using Serilog;

namespace Maestro.Core.Infrastructure;

// TODO:
// - [X] Dispatch messages to both Mediator (for in-memory stuff) and SignalR (when connected); Disregard permissions for now
// - [X] Invoke SignalR methods when initializing the connection, ensure it's all synchronous to prevent dirty states
// - [X] Encapsulate the in-memory sequence, the SignalR connection, and the UI stuff into a Session object
// - [X] Test with two clients connected at once, what breaks?
// - [X] Restrict access to certain functions from certain clients
// - [X] Show information messages when things happen
// - [X] Coordination messages

public class MaestroConnection : IAsyncDisposable
{
    readonly CancellationTokenSource _rootCancellationTokenSource = new();
    readonly string _partition;
    readonly string _airportIdentifier;
    readonly string _callsign;
    readonly Role _role;
    readonly HubConnection _hubConnection;
    readonly IMediator _mediator;
    readonly ILogger _logger;

    public bool IsConnected => _hubConnection.State is not HubConnectionState.Disconnected;
    public string Partition => _partition;
    public string AirportIdentifier => _airportIdentifier;
    public string Callsign => _callsign;
    public Role Role => _role;

    public MaestroConnection(
        ServerConfiguration serverConfiguration,
        string partition,
        string airportIdentifier,
        string callsign,
        IMediator mediator,
        ILogger logger)
    {
        _partition = partition;
        _airportIdentifier = airportIdentifier;
        _callsign = callsign;
        _role = RoleHelper.GetRoleFromCallsign(callsign);

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(serverConfiguration.Uri + $"?partition={partition}&airportIdentifier={airportIdentifier}&callsign={callsign}&role={_role}")
            .WithServerTimeout(TimeSpan.FromSeconds(serverConfiguration.TimeoutSeconds))
            .WithAutomaticReconnect()
            .WithStatefulReconnect()
            .AddNewtonsoftJsonProtocol(x =>
            {
                x.PayloadSerializerSettings.ContractResolver = new DefaultContractResolver();
                x.PayloadSerializerSettings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;
            })
            .Build();

        _mediator = mediator;
        _logger = logger;

        SubscribeToNotifications();
        SubscribeToConnectionEvents();
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        if (IsConnected)
            throw new MaestroException("Cannot start the connection when already connected.");

        try
        {
            await _hubConnection.StartAsync(cancellationToken);
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
            await _hubConnection.StopAsync(cancellationToken);
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
        var response = await _hubConnection.InvokeAsync<RelayResponse>(methodName, message, cancellationToken);
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

    void SubscribeToNotifications()
    {
        _hubConnection.On<ConnectionInitializedNotification>("ConnectionInitialized", async notification =>
        {
            if (notification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(notification, GetMessageCancellationToken());
        });

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

        _hubConnection.On<CoordinationNotification>("Coordination", async coordinationNotification =>
        {
            if (coordinationNotification.AirportIdentifier != _airportIdentifier)
                return;

            _logger.Information("Received coordination message {Message}", coordinationNotification.Message);
            await _mediator.Publish(coordinationNotification, GetMessageCancellationToken());
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

        _hubConnection.On<RequestEnvelope<ManualDelayRequest>, RelayResponse>("ManualDelay", async envelope =>
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
            await _mediator.Publish(new SessionDisconnectedNotification(_airportIdentifier, IsReady: true), CancellationToken.None);

            // If the connection was closed due to an error, take ownership of the sequence and report the error
            if (exception != null && !_rootCancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error(exception, "Connection for {AirportIdentifier} lost", _airportIdentifier);
                // await _mediator.Publish(new OwnershipGrantedNotification(_airportIdentifier), CancellationToken.None);
                await _mediator.Publish(new ErrorNotification(exception), CancellationToken.None);

                // BUG: UI still says "READY" after disconnecting here. Need to remove connection details and connection from Session
            }
            else
            {
                _logger.Information("Connection for {AirportIdentifier} closed", _airportIdentifier);
            }
        };

        _hubConnection.Reconnecting += async exception =>
        {
            _logger.Warning(exception, "Connection for {AirportIdentifier} reconnecting", _airportIdentifier);
            await _mediator.Publish(new SessionReconnectingNotification(_airportIdentifier), CancellationToken.None);
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.Information(
                "Connection for {AirportIdentifier} reconnected with ConnectionId {ConnectionId}",
                _airportIdentifier, connectionId);
            return Task.CompletedTask;
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
