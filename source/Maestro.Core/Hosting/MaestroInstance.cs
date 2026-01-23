using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Sessions;
using MediatR;

namespace Maestro.Core.Hosting;

public class MaestroInstance : IAsyncDisposable
{
    readonly IMediator _mediator;
    readonly BackgroundTask _sequenceCleanerTask;

    public SemaphoreSlim Semaphore { get; } = new(1, 1);

    public string AirportIdentifier { get; }
    public Session Session { get; }

    public MaestroInstance(string airportIdentifier, Session session, IMediator mediator)
    {
        AirportIdentifier = airportIdentifier;
        Session = session;
        _mediator = mediator;

        _sequenceCleanerTask = new BackgroundTask(FlightCleanUpTask);
        _sequenceCleanerTask.Start();
    }

    async Task FlightCleanUpTask(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await _mediator.Send(new CleanUpFlightsRequest(AirportIdentifier), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Ignored, task is cancelling
        }
        catch (Exception exception)
        {
            // TODO: Log error
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sequenceCleanerTask.DisposeAsync();
        Semaphore.Dispose();
    }
}
