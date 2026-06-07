using Maestro.Contracts.Flights;
using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using Maestro.Core.Sessions.Contracts;
using MediatR;
using Serilog;

namespace Maestro.Core.Sessions;

public class Session : IAsyncDisposable
{
    int _dummyCounter = 1;
    readonly IMediator _mediator;
    readonly ILogger _logger;
    readonly BackgroundTask _backgroundTask;

    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public string AirportIdentifier => Sequence.AirportIdentifier;
    public List<Flight> DeSequencedFlights { get; } = new();
    public Sequence Sequence { get; }
    public LandingStatistics LandingStatistics { get; }

    /// <summary>
    /// The latest flight data received from the FDP, keyed by callsign.
    /// Used to look up flight data when inserting pending flights into the sequence.
    /// </summary>
    public Dictionary<string, FlightDataRecord> FlightDataRecords { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Session(Sequence sequence, IMediator mediator, ILogger logger)
    {
        Sequence = sequence;
        LandingStatistics = new LandingStatistics(logger);

        _mediator = mediator;
        _logger = logger;

        _backgroundTask = new BackgroundTask(RunProcesses);
        _backgroundTask.Start();
    }

    // TODO: Move dummy stuff to a separate service
    public string NewDummyCallsign()
    {
        return $"****{_dummyCounter++:00}*";
    }

    public SessionDto Snapshot()
    {
        return new SessionDto
        {
            AirportIdentifier = AirportIdentifier,
            DeSequencedFlights = DeSequencedFlights.Select(f => f.ToDto(Sequence)).ToArray(),
            Sequence = Sequence.ToDto(),
            DummyCounter = _dummyCounter,
            LandingStatistics = LandingStatistics.Snapshot(),
            FlightDataRecords = FlightDataRecords.Values.ToArray()
        };
    }

    public void Restore(SessionDto dto)
    {
        _dummyCounter = dto.DummyCounter;

        FlightDataRecords.Clear();
        foreach (var data in dto.FlightDataRecords)
            FlightDataRecords[data.Callsign] = data;

        DeSequencedFlights.Clear();
        DeSequencedFlights.AddRange(dto.DeSequencedFlights.Select(f => new Flight(f)));

        Sequence.Restore(dto.Sequence);
        LandingStatistics.Restore(dto.LandingStatistics);
    }

    async Task RunProcesses(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _mediator.Send(new TrySwapConfigurationRequest(AirportIdentifier), cancellationToken);
                await _mediator.Send(new ProcessFlightsRequest(AirportIdentifier), cancellationToken);
                await _mediator.Send(new CleanUpFlightsRequest(AirportIdentifier), cancellationToken);

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected during shutdown
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Background maintenance failed for {AirportIdentifier}", AirportIdentifier);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _backgroundTask.DisposeAsync();
        Semaphore.Dispose();
    }
}
