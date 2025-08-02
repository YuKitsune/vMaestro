using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    bool CanSequenceFor(string airportIdentifier);
    Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken);
    SequenceMessage GetReadOnlySequence(string airportIdentifier);
}

public interface IExclusiveSequence : IDisposable
{
    Sequence Sequence { get; }
}

public class ExclusiveSequence(Sequence sequence, SemaphoreSlim semaphoreSlim) : IExclusiveSequence
{
    public Sequence Sequence { get; } = sequence;

    public void Dispose()
    {
        semaphoreSlim.Release();
    }
}

public class SequenceProvider : ISequenceProvider
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;

    readonly List<SequenceLockPair> _sequences = [];

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
            // TODO: Don't start sequencing until the user requests it
            var sequence = new Sequence(
                airportConfiguration,
                airportConfiguration.RunwayModes.First());
            var semaphoreSlim = new SemaphoreSlim(1, 1);

            var pair = new SequenceLockPair
            {
                Sequence = sequence,
                Semaphore = semaphoreSlim
            };

            _sequences.Add(pair);
        }
    }

    public bool CanSequenceFor(string airportIdentifier)
    {
        return _sequences.Any(s => s.Sequence.AirportIdentifier == airportIdentifier);
    }

    public async Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken)
    {
        var pair = _sequences.SingleOrDefault(x => x.Sequence.AirportIdentifier == airportIdentifier);

        if (pair is null)
            throw new MaestroException($"Sequence for {airportIdentifier} not found");

        await pair.Semaphore.WaitAsync(cancellationToken);
        return new ExclusiveSequence(pair.Sequence, pair.Semaphore);
    }

    public SequenceMessage GetReadOnlySequence(string airportIdentifier)
    {
        var pair = _sequences.SingleOrDefault(x => x.Sequence.AirportIdentifier == airportIdentifier);
        if (pair is null)
            throw new MaestroException($"Sequence for {airportIdentifier} not found");

        return pair.Sequence.ToMessage();
    }

    class SequenceLockPair
    {
        public required Sequence Sequence { get; init; }
        public required SemaphoreSlim Semaphore { get; init; }
    }
}
