using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using Maestro.Core.Integration;
using Maestro.Core.Model;
using NSubstitute;

namespace Maestro.Core.Tests.Builders;

public class SequenceBuilder(AirportConfiguration airportConfiguration)
{
    RunwayMode? _runwayMode;
    IArrivalLookup _arrivalLookup = Substitute.For<IArrivalLookup>();
    IClock _clock = Substitute.For<IClock>();

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

    public Sequence Build()
    {
        var sequence = new Sequence(airportConfiguration, _arrivalLookup, _clock);
        sequence.ChangeRunwayMode(_runwayMode ?? new RunwayMode(airportConfiguration.RunwayModes.First()));

        return sequence;
    }
}
