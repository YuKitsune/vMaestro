using Maestro.Core.Extensions;
using Maestro.Core.Messages;
using Maestro.Core.Model;

namespace Maestro.Core.Tests.Mocks;

public class MockSequenceProvider(Sequence sequence) : ISequenceProvider
{
    public bool CanSequenceFor(string airportIdentifier)
    {
        return sequence.AirportIdentifier == airportIdentifier;
    }

    public Task<IExclusiveSequence> GetSequence(string airportIdentifier, CancellationToken cancellationToken)
    {
        if (!CanSequenceFor(airportIdentifier))
            throw new InvalidOperationException($"Cannot sequence for airport: {airportIdentifier}");

        return Task.FromResult<IExclusiveSequence>(new MockExclusiveSequence(sequence));
    }

    public SequenceMessage GetReadOnlySequence(string airportIdentifier)
    {
        if (!CanSequenceFor(airportIdentifier))
            throw new InvalidOperationException($"Cannot get read-only sequence for airport: {airportIdentifier}");

        return sequence.ToMessage();
    }

    class MockExclusiveSequence(Sequence sequence) : IExclusiveSequence
    {
        public Sequence Sequence { get; } = sequence;

        public void Dispose()
        {
            // No-op
        }
    }
}
