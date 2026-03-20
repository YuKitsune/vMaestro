using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class PeerInfoSerializationTests
{
    [Fact]
    public void PeerInfo_Serialization_Json()
    {
        var original = CreatePeerInfo();
        VerifyJsonSnapshot(original, "PeerInfo.json");
    }

    [Fact]
    public void PeerInfo_Serialization_MessagePack()
    {
        var original = CreatePeerInfo();
        VerifyMessagePackSnapshot(original, "PeerInfo.msgpack");
    }
}
