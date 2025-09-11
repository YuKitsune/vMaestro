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
// - [ ] Restrict access to certain functions from certain clients
// - [ ] Show information messages when things happen
// - [ ] Coordination messages

public class MaestroConnection : IAsyncDisposable
{
    readonly CancellationTokenSource _rootCancellationTokenSource = new();
    readonly string _partition;
    readonly string _airportIdentifier;
    readonly HubConnection _hubConnection;
    readonly IMediator _mediator;
    readonly ILogger _logger;

    public bool IsConnected => _hubConnection.State is not HubConnectionState.Disconnected;

    public MaestroConnection(
        string partition,
        string airportIdentifier,
        HubConnection hubConnection,
        IMediator mediator,
        ILogger logger)
    {
        _partition = partition;
        _airportIdentifier = airportIdentifier;
        _hubConnection = hubConnection;
        _mediator = mediator;
        _logger = logger;
        AirportIdentifier = airportIdentifier;
        SubscribeToNotifications();
        SubscribeToConnectionEvents();
    }

    public string AirportIdentifier { get; }

    // TODO: Find a better place for this
    public record SequenceStartResult(bool OwnsSequence, SequenceMessage? Sequence);

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
            var response = await _hubConnection.InvokeAsync<JoinSequenceResponse>(
                "JoinSequence",
                new JoinSequenceRequest(_partition, AirportIdentifier, position),
                cancellationToken);

            return new SequenceStartResult(response.OwnsSequence, response.Sequence);
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
            await _hubConnection.InvokeAsync(
                "LeaveSequence",
                new LeaveSequenceRequest(AirportIdentifier),
                cancellationToken);
        }
        catch (Exception exception)
        {
            throw new MaestroException("Failed to leave sequence", exception);
        }
    }

    public async Task Send<T>(T message, CancellationToken cancellationToken)
        where T : class
    {
        await _hubConnection.InvokeAsync(message.GetType().Name, message, cancellationToken);
    }

    void SubscribeToNotifications()
    {
        _hubConnection.On<OwnershipGrantedNotification>("OwnershipGranted", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<SequenceUpdatedNotification>("SequenceUpdatedNotification", async sequenceUpdatedNotification =>
        {
            if (sequenceUpdatedNotification.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Publish(sequenceUpdatedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<FlightUpdatedNotification>("FlightUpdatedNotification", async flightUpdatedNotification =>
        {
            if (flightUpdatedNotification.Destination != _airportIdentifier)
                return;

            await _mediator.Publish(flightUpdatedNotification, GetMessageCancellationToken());
        });

        _hubConnection.On<InsertFlightRequest>("InsertFlightRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<InsertDepartureRequest>("InsertDepartureRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<MoveFlightRequest>("MoveFlightRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<SwapFlightsRequest>("SwapFlightsRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<RemoveRequest>("RemoveRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<DesequenceRequest>("DesequenceRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<MakePendingRequest>("MakePendingRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<MakeStableRequest>("MakeStableRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<RecomputeRequest>("RecomputeRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ResumeSequencingRequest>("ResumeSequencingRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ZeroDelayRequest>("ZeroDelayRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ChangeRunwayRequest>("ChangeRunwayRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ChangeRunwayModeRequest>("ChangeRunwayModeRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ChangeFeederFixEstimateRequest>("ChangeFeederFixEstimateRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<CreateSlotRequest>("CreateSlotRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<ModifySlotRequest>("ModifySlotRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });

        _hubConnection.On<DeleteSlotRequest>("DeleteSlotRequest", async request =>
        {
            if (request.AirportIdentifier != _airportIdentifier)
                return;

            await _mediator.Send(request, GetMessageCancellationToken());
        });
    }

    void SubscribeToConnectionEvents()
    {
        _hubConnection.Closed += async (exception) =>
        {
            // If the connection was closed due to an error, take ownership of the sequence and report the error
            if (exception != null && !_rootCancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error(exception, "Connection for {AirportIdentifier} lost", _airportIdentifier);
                await _mediator.Publish(new OwnershipGrantedNotification(_airportIdentifier), CancellationToken.None);
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

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.Information(
                "Connection for {AirportIdentifier} reconnected with connectionId {ConnectionId}",
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

    public async ValueTask DisposeAsync()
    {
        await _hubConnection.DisposeAsync();
        _rootCancellationTokenSource.Cancel();
        _rootCancellationTokenSource.Dispose();
    }
}
