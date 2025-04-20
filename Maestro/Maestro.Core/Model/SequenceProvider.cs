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
    readonly IScheduler _scheduler;
    readonly IEstimateProvider _estimateProvider;
    readonly IMediator _mediator;
    readonly IClock _clock;
    readonly IRunwayAssigner _runwayAssigner;

    readonly List<Sequence> _sequences = [];

    public SequenceProvider(
        IAirportConfigurationProvider airportConfigurationProvider,
        IMediator mediator,
        IClock clock,
        IEstimateProvider estimateProvider,
        IScheduler scheduler,
        IRunwayAssigner runwayAssigner)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _mediator = mediator;
        _clock = clock;
        _estimateProvider = estimateProvider;
        _scheduler = scheduler;
        _runwayAssigner = runwayAssigner;

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
                _mediator,
                _clock,
                _estimateProvider,
                _scheduler,
                _runwayAssigner);
            
            sequence.Start();
        
            _sequences.Add(sequence);
        }
    }

    public Sequence? TryGetSequence(string airportIdentifier)
    {
        return _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
    }
}
