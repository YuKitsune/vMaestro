using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class DesequenceRequestSerializationTests
{
    [Fact]
    public void DesequenceRequest_Serialization_Json()
    {
        var original = CreateDesequenceRequest();
        VerifyJsonSnapshot(original, "DesequenceRequest.json");
    }

    [Fact]
    public void DesequenceRequest_Serialization_MessagePack()
    {
        var original = CreateDesequenceRequest();
        VerifyMessagePackSnapshot(original, "DesequenceRequest.msgpack");
    }
}
