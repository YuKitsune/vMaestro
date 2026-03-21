using Maestro.Contracts.Sessions;
using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Handlers;
using Maestro.Core.Model;

namespace Maestro.Core.Sessions;

public class Session
{
    int _dummyCounter = 1;

    public string AirportIdentifier => Sequence.AirportIdentifier;
    public List<Flight> PendingFlights { get; } = new();
    public List<Flight> DeSequencedFlights { get; } = new();
    public Sequence Sequence { get; private set; }
    public LandingStatistics LandingStatistics { get; } = new();

    public int UpperWindAltitude { get; }
    public Wind UpperWind { get; set; } = new(0, 0);
    public Wind SurfaceWind { get; set; } = new(0, 0);
    public bool ManualWind { get; set; }

    public Session(AirportConfiguration airportConfiguration, Sequence sequence)
    {
        UpperWindAltitude = airportConfiguration.UpperWindAltitude;
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
            DummyCounter = _dummyCounter,
            LandingStatistics = LandingStatistics.Snapshot(),
            SurfaceWind = new WindDto(SurfaceWind.Direction, SurfaceWind.Speed),
            UpperWind = new WindDto(UpperWind.Direction, UpperWind.Speed),
            ManualWind = ManualWind
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
        LandingStatistics.Restore(dto.LandingStatistics);

        SurfaceWind = new Wind(dto.SurfaceWind.Direction, dto.SurfaceWind.Speed);
        UpperWind = new Wind(dto.UpperWind.Direction, dto.UpperWind.Speed);
        ManualWind = dto.ManualWind;
    }
}
