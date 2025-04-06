using System.Runtime.CompilerServices;
using Maestro.Core.Configuration;
using MediatR;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    Sequence? TryGetSequence(string airportIdentifier);
}

public class SequenceProvider : ISequenceProvider
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly IMediator _mediator;

    readonly List<Sequence> _sequences = [];

    public SequenceProvider(IAirportConfigurationProvider airportConfigurationProvider, IMediator mediator)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _mediator = mediator;
        InitializeSequences();
    }

    void InitializeSequences()
    {
        var airportConfigurations = _airportConfigurationProvider
            .GetAirportConfigurations();
        
        foreach (var airportConfiguration in airportConfigurations)
        {
            var sequence = new Sequence(
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
        }
    }

    public Sequence? TryGetSequence(string airportIdentifier)
    {
        return _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
    }
}
