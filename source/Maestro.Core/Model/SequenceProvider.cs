using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Infrastructure;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    bool CanSequenceFor(string airportIdentifier);
    Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken);
    SlotBasedSequenceDto GetReadOnlySequence(string airportIdentifier);
}

public interface IExclusiveSequence : IDisposable
{
    SlotBasedSequence Sequence { get; }
}

public class ExclusiveSequence(SlotBasedSequence sequence, SemaphoreSlim semaphoreSlim) : IExclusiveSequence
{
    public SlotBasedSequence Sequence { get; } = sequence;

    public void Dispose()
    {
        semaphoreSlim.Release();
    }
}

public class SequenceProvider : ISequenceProvider
{
    readonly IAirportConfigurationProvider _airportConfigurationProvider;
    readonly IClock _clock;

    readonly List<SequenceLockPair> _sequences = [];

    public SequenceProvider(IAirportConfigurationProvider airportConfigurationProvider, IClock clock)
    {
        _airportConfigurationProvider = airportConfigurationProvider;
        _clock = clock;
        InitializeSequences();
    }

    void InitializeSequences()
    {
        var airportConfigurations = _airportConfigurationProvider
            .GetAirportConfigurations();

        foreach (var airportConfiguration in airportConfigurations)
        {
            // TODO: Don't start sequencing until user requests it.
            var sequence = new SlotBasedSequence(
                airportConfiguration,
                airportConfiguration.RunwayModes.First(),
                _clock.UtcNow());

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

    public SlotBasedSequenceDto GetReadOnlySequence(string airportIdentifier)
    {
        var pair = _sequences.SingleOrDefault(x => x.Sequence.AirportIdentifier == airportIdentifier);
        if (pair is null)
            throw new MaestroException($"Sequence for {airportIdentifier} not found");

        return pair.Sequence.ToDto();
    }

    class SequenceLockPair
    {
        public required SlotBasedSequence Sequence { get; init; }
        public required SemaphoreSlim Semaphore { get; init; }
    }
}
