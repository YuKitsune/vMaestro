using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Model;

public class SequenceProvider(IAirportConfigurationProvider airportConfigurationProvider, IMediator mediator)
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider = airportConfigurationProvider;
    readonly IMediator _mediator = mediator;

    readonly List<Sequence> _sequences = [];

    public Sequence GetOrCreateSequence(string airportIdentifier)
    {
        var sequence = _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
        if (sequence is not null)
            return sequence;
        
        var airportConfiguration = _airportConfigurationProvider
            .GetAirportConfigurations()
            .SingleOrDefault(x => x.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for airport {airportIdentifier}");

        sequence = new Sequence(
            airportConfiguration.Identifier,
            airportConfiguration.RunwayModes.Select(rm =>
                new RunwayMode
                {
                    Identifier = rm.Identifier,
                    LandingRates = rm.Runways.ToDictionary(
                        r => r.Identifier,
                        r => TimeSpan.FromSeconds(r.DefaultLandingRateSeconds))
                })
                .ToArray(),
            airportConfiguration.FeederFixes,
            _mediator);
        
        _sequences.Add(sequence);
        return sequence;
    }

    public bool TryGetSequence(
        string airportIdentifier,
        out Sequence? sequence)
    {
        sequence = _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
        return sequence is not null;
    }
}
