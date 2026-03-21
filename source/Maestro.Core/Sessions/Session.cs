using Maestro.Contracts.Sessions;
using Maestro.Core.Extensions;
using Maestro.Core.Model;

namespace Maestro.Core.Sessions;

public class Session
{
    int _dummyCounter = 1;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public List<Flight> PendingFlights { get; } = new();
    public List<Flight> DeSequencedFlights { get; } = new();
    public Sequence Sequence { get; private set; }

    public Session(Sequence sequence)
    {
        Sequence = sequence;
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
            PendingFlights = PendingFlights.Select(f => f.ToDto(Sequence)).ToArray(),
            DeSequencedFlights = DeSequencedFlights.Select(f => f.ToDto(Sequence)).ToArray(),
            Sequence = Sequence.ToDto(),
            DummyCounter = _dummyCounter
        };
    }

    public void Restore(SessionDto dto)
    {
        _dummyCounter = dto.DummyCounter;

        PendingFlights.Clear();
        PendingFlights.AddRange(dto.PendingFlights.Select(f => new Flight(f)));

        DeSequencedFlights.Clear();
        DeSequencedFlights.AddRange(dto.DeSequencedFlights.Select(f => new Flight(f)));

        Sequence.Restore(dto.Sequence);
    }
}
