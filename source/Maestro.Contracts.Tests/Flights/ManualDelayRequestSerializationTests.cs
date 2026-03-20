using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ManualDelayRequestSerializationTests
{
    [Fact]
    public void ManualDelayRequest_Serialization_Json()
    {
        var original = CreateManualDelayRequest();
        VerifyJsonSnapshot(original, "ManualDelayRequest.json");
    }

    [Fact]
    public void ManualDelayRequest_Serialization_MessagePack()
    {
        var original = CreateManualDelayRequest();
        VerifyMessagePackSnapshot(original, "ManualDelayRequest.msgpack");
    }
}
