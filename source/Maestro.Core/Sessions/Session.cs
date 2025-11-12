using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;
using Maestro.Core.Model;
using SequenceMessage = Maestro.Core.Messages.SequenceMessage;

namespace Maestro.Core.Sessions;

public interface ISession : IDisposable
{
    string AirportIdentifier { get; }
    Sequence Sequence { get; }
    string Position { get; }
    SemaphoreSlim Semaphore { get; }
    bool IsActive { get; }

    Task Start(string position, CancellationToken cancellationToken);
    Task Stop(CancellationToken cancellationToken);
}

public class Session : ISession, IDisposable
{
    int _dummyCounter = 1;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public List<Flight> PendingFlights { get; } = new();
    public List<Flight> DeSequencedFlights { get; } = new();
    public Sequence Sequence { get; private set; }
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public string Position { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public Session(AirportConfiguration airportConfiguration, IArrivalLookup arrivalLookup, IClock clock)
    {
        Sequence = new Sequence(airportConfiguration, arrivalLookup, clock);
    }

    public Task Start(string position, CancellationToken cancellationToken)
    {
        Position = position;
        IsActive = true;
        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken)
    {
        Position = string.Empty;
        IsActive = false;
        return Task.CompletedTask;
    }

    // TODO: Move dummy stuff to a separate service
    public string NewDummyCallsign()
    {
        return $"****{_dummyCounter++:00}*";
    }

    public void Dispose()
    {
        Semaphore.Dispose();
    }

    public SessionMessage Snapshot()
    {
        return new SessionMessage
        {
            AirportIdentifier = AirportIdentifier,
            PendingFlights = PendingFlights.Select(f => f.ToMessage(Sequence)).ToArray(),
            DeSequencedFlights = DeSequencedFlights.Select(f => f.ToMessage(Sequence)).ToArray(),
            Sequence = Sequence.ToMessage(),
            DummyCounter = _dummyCounter
        };
    }

    public void Restore(SessionMessage message)
    {
        _dummyCounter = message.DummyCounter;

        PendingFlights.Clear();
        PendingFlights.AddRange(message.PendingFlights.Select(f => new Flight(f)));

        DeSequencedFlights.Clear();
        DeSequencedFlights.AddRange(message.DeSequencedFlights.Select(f => new Flight(f)));

        Sequence.Restore(message.Sequence);
    }
}

public class SessionMessage
{
    public required string AirportIdentifier { get; init; }
    public required FlightMessage[] PendingFlights { get; init; }
    public required FlightMessage[] DeSequencedFlights { get; init; }
    public required SequenceMessage Sequence { get; init; }
    public required int DummyCounter { get; init; }
}
