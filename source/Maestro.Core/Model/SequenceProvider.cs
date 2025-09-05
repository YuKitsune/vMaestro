using Maestro.Core.Configuration;
using Maestro.Core.Extensions;
using Maestro.Core.Messages;

namespace Maestro.Core.Model;

public interface ISequenceProvider
{
    string[] ActiveSequences { get; }
    Task InitializeSequence(
        string airportIdentifier,
        RunwayMode runwayMode,
        CancellationToken cancellationToken);
    Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken);
    SequenceMessage GetReadOnlySequence(string airportIdentifier);
    Task TerminateSequence(string airportIdentifier, CancellationToken cancellationToken);
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

public class SequenceProvider(IAirportConfigurationProvider airportConfigurationProvider) : ISequenceProvider
{
    readonly SemaphoreSlim _semaphore = new(1, 1);

    readonly List<SequenceLockPair> _sequences = [];

    public string[] ActiveSequences => _sequences.Select(s => s.Sequence.AirportIdentifier).ToArray();

    public async Task InitializeSequence(
        string airportIdentifier,
        RunwayMode runwayMode,
        CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);

        if (_sequences.Any(s => s.Sequence.AirportIdentifier == airportIdentifier))
            throw new MaestroException($"Sequence for {airportIdentifier} already initialized");

        var airportConfiguration = airportConfigurationProvider
            .GetAirportConfigurations()
            .FirstOrDefault(a => a.Identifier == airportIdentifier);
        if (airportConfiguration is null)
            throw new MaestroException($"No configuration found for {airportIdentifier}");

        var sequence = new Sequence(airportConfiguration, runwayMode);
        var semaphoreSlim = new SemaphoreSlim(1, 1);
        var pair = new SequenceLockPair
        {
            Sequence = sequence,
            Semaphore = semaphoreSlim
        };

        _sequences.Add(pair);
    }

    public async Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
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

    public async Task TerminateSequence(string airportIdentifier, CancellationToken cancellationToken)
    {
        using var _ = await _semaphore.LockAsync(cancellationToken);
        var pair = _sequences.SingleOrDefault(x => x.Sequence.AirportIdentifier == airportIdentifier);
        if (pair is null)
            throw new MaestroException($"Sequence for {airportIdentifier} not found");

        _sequences.Remove(pair);
        pair.Semaphore.Dispose();
    }

    class SequenceLockPair
    {
        public required Sequence Sequence { get; init; }
        public required SemaphoreSlim Semaphore { get; init; }
    }
}
