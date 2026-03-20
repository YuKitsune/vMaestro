using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class RecomputeRequestSerializationTests
{
    [Fact]
    public void RecomputeRequest_Serialization_Json()
    {
        var original = CreateRecomputeRequest();
        VerifyJsonSnapshot(original, "RecomputeRequest.json");
    }

    [Fact]
    public void RecomputeRequest_Serialization_MessagePack()
    {
        var original = CreateRecomputeRequest();
        VerifyMessagePackSnapshot(original, "RecomputeRequest.msgpack");
    }
}
