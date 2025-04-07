using Maestro.Core.Configuration;
using Maestro.Core.Infrastructure;
using MediatR;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    Sequence? TryGetSequence(string airportIdentifier);
}

public class SequenceProvider : ISequenceProvider
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly ISeparationRuleProvider _separationRuleProvider;
    readonly IPerformanceLookup _performanceLookup;
    readonly IMediator _mediator;
    readonly IClock _clock;

    readonly List<Sequence> _sequences = [];

    public SequenceProvider(
        IAirportConfigurationProvider airportConfigurationProvider,
        ISeparationRuleProvider separationRuleProvider,
        IPerformanceLookup performanceLookup,
        IMediator mediator,
        IClock clock)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _separationRuleProvider = separationRuleProvider;
        _performanceLookup = performanceLookup;
        _mediator = mediator;
        _clock = clock;
        
        InitializeSequences();
    }

    void InitializeSequences()
    {
        var airportConfigurations = _airportConfigurationProvider
            .GetAirportConfigurations();
        
        foreach (var airportConfiguration in airportConfigurations)
        {
            var sequence = new Sequence(
                airportConfiguration,
                _separationRuleProvider,
                _performanceLookup,
                _mediator,
                _clock);
            
            sequence.Start();
        
            _sequences.Add(sequence);
        }
    }

    public Sequence? TryGetSequence(string airportIdentifier)
    {
        return _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
    }
}
