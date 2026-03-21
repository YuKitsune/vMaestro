using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ChangeFeederFixEstimateRequestSerializationTests
{
    [Fact]
    public void ChangeFeederFixEstimateRequest_Serialization_Json()
    {
        var original = CreateChangeFeederFixEstimateRequest();
        VerifyJsonSnapshot(original, "ChangeFeederFixEstimateRequest.json");
    }

    [Fact]
    public void ChangeFeederFixEstimateRequest_Serialization_MessagePack()
    {
        var original = CreateChangeFeederFixEstimateRequest();
        VerifyMessagePackSnapshot(original, "ChangeFeederFixEstimateRequest.msgpack");
    }
}
