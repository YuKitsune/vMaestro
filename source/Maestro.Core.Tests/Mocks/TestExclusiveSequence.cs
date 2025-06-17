using Maestro.Core.Model;

namespace Maestro.Core.Tests.Mocks;

public class TestExclusiveSequence(Sequence sequence) : IExclusiveSequence
{
    public Sequence Sequence { get; } = sequence;
    
    public void Dispose()
    {
        // No-op.
    }
}