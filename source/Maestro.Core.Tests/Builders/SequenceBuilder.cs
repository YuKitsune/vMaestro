using Maestro.Core.Configuration;
using Maestro.Core.Model;
using NSubstitute;

namespace Maestro.Core.Tests.Builders;

public class SequenceBuilder(AirportConfiguration airportConfiguration)
{
    readonly List<Flight> _flights = new();
    RunwayMode? _runwayMode;
    readonly IScheduler _scheduler = Substitute.For<IScheduler>();

    public SequenceBuilder WithRunwayMode(RunwayMode runwayMode)
    {
        _runwayMode = runwayMode;
        return this;
    }

    public SequenceBuilder WithRunwayMode(RunwayModeConfiguration runwayModeConfiguration)
    {
        _runwayMode = new RunwayMode(runwayModeConfiguration);
        return this;
    }

    public SequenceBuilder WithSingleRunway(string runwayIdentifier, TimeSpan landingRate)
    {
        return WithRunwayMode(
            new RunwayMode(
                new RunwayModeConfiguration
                {
                    Identifier = runwayIdentifier,
                    Runways =
                    [
                        new RunwayConfiguration
                            { Identifier = runwayIdentifier, LandingRateSeconds = (int)landingRate.TotalSeconds }
                    ]
                }));
    }

    public SequenceBuilder WithFlight(Flight flight)
    {
        _flights.Add(flight);
        return this;
    }

    public Sequence Build()
    {
        var sequence = new Sequence(airportConfiguration);
        if (_runwayMode is not null)
            sequence.ChangeRunwayMode(_runwayMode, Substitute.For<IScheduler>());

        foreach (var flight in _flights)
        {
            sequence.AddFlight(flight, _scheduler);
        }

        return sequence;
    }
}
