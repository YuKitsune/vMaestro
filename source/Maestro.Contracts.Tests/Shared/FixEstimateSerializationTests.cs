using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Shared;

public class FixEstimateSerializationTests
{
    [Fact]
    public void FixEstimate_Serialization_Json()
    {
        var original = CreateFixEstimate();
        VerifyJsonSnapshot(original, "FixEstimate.json");
    }

    [Fact]
    public void FixEstimate_Serialization_MessagePack()
    {
        var original = CreateFixEstimate();
        VerifyMessagePackSnapshot(original, "FixEstimate.msgpack");
    }
}
