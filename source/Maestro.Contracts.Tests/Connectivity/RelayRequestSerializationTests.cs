using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class RelayRequestSerializationTests
{
    [Fact]
    public void RelayRequest_Serialization_Json()
    {
        var original = CreateRelayRequest();
        VerifyJsonSnapshot(original, "RelayRequest.json");
    }

    [Fact]
    public void RelayRequest_Serialization_MessagePack()
    {
        var original = CreateRelayRequest();
        VerifyMessagePackSnapshot(original, "RelayRequest.msgpack");
    }
}
