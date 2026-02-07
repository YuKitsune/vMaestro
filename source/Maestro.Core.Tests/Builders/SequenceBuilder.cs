using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Model;
using NSubstitute;

namespace Maestro.Core.Tests.Builders;

public class SequenceBuilder(AirportConfiguration airportConfiguration)
{
    RunwayMode? _runwayMode;
    IArrivalLookup _arrivalLookup = Substitute.For<IArrivalLookup>();
    IClock _clock = Substitute.For<IClock>();
    List<Flight> _flights = [];

    public SequenceBuilder WithArrivalLookup(IArrivalLookup arrivalLookup)
    {
        _arrivalLookup = arrivalLookup;
        return this;
    }

    public SequenceBuilder WithClock(IClock clock)
    {
        _clock = clock;
        return this;
    }

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
        return WithSingleRunway(runwayIdentifier, string.Empty, landingRate);
    }

    public SequenceBuilder WithSingleRunway(string runwayIdentifier, string approachType, TimeSpan landingRate)
    {
        return WithRunwayMode(
            new RunwayMode(
                new RunwayModeConfiguration
                {
                    Identifier = runwayIdentifier,
                    Runways =
                    [
                        new RunwayConfiguration
                        {
                            Identifier = runwayIdentifier,
                            ApproachType = approachType,
                            LandingRateSeconds = (int)landingRate.TotalSeconds,
                            FeederFixes = []
                        }
                    ]
                }));
    }

    public SequenceBuilder WithFlight(Flight flight)
    {
        _flights.Add(flight);
        return this;
    }

    public SequenceBuilder WithFlightsInOrder(params Flight[] flights)
    {
        _flights.AddRange(flights);
        return this;
    }

    public Sequence Build()
    {
        var sequence = new Sequence(airportConfiguration, _arrivalLookup, _clock);
        sequence.ChangeRunwayMode(_runwayMode ?? new RunwayMode(airportConfiguration.RunwayModes.First()));
        for (var i = 0; i < _flights.Count; i++)
        {
            sequence.Insert(i, _flights[i]);
        }

        return sequence;
    }
}
