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

    public SequenceBuilder WithSingleRunway(string runwayIdentifier, TimeSpan landingRate)
    {
        var runwayMode = new RunwayMode
        {
            Identifier = runwayIdentifier,
            Runways =
            [
                new RunwayConfiguration
                    { Identifier = runwayIdentifier, LandingRateSeconds = (int)landingRate.TotalSeconds }
            ]
        };

        return WithRunwayMode(runwayMode);
    }

    public SequenceBuilder WithFlight(Flight flight)
    {
        _flights.Add(flight);
        return this;
    }

    public Sequence Build()
    {
        var sequence = new Sequence(
            airportConfiguration,
            _runwayMode ?? airportConfiguration.RunwayModes.First());

        foreach (var flight in _flights)
        {
            sequence.AddFlight(flight, _scheduler);
        }

        return sequence;
    }
}
