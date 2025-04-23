using Maestro.Core.Configuration;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    Sequence? TryGetSequence(string airportIdentifier);
}

public class SequenceProvider : ISequenceProvider
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;

    readonly List<Sequence> _sequences = [];

    public SequenceProvider(IAirportConfigurationProvider airportConfigurationProvider)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        InitializeSequences();
    }

    void InitializeSequences()
    {
        var airportConfigurations = _airportConfigurationProvider
            .GetAirportConfigurations();
        
        foreach (var airportConfiguration in airportConfigurations)
        {
            var sequence = new Sequence(airportConfiguration);
            _sequences.Add(sequence);
        }
    }

    public Sequence? TryGetSequence(string airportIdentifier)
    {
        return _sequences.SingleOrDefault(x => x.AirportIdentifier == airportIdentifier);
    }
}
