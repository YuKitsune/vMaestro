using Maestro.Core.Model;

namespace Maestro.Core.Tests.Mocks;

public class TestExclusiveSequence(SlotBasedSequence sequence) : IExclusiveSequence
{
    public SlotBasedSequence Sequence { get; } = sequence;

    public void Dispose()
    {
        // No-op.
    }
}
