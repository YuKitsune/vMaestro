using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class PeerDisconnectedNotificationSerializationTests
{
    [Fact]
    public void PeerDisconnectedNotification_Serialization_Json()
    {
        var original = CreatePeerDisconnectedNotification();
        VerifyJsonSnapshot(original, "PeerDisconnectedNotification.json");
    }

    [Fact]
    public void PeerDisconnectedNotification_Serialization_MessagePack()
    {
        var original = CreatePeerDisconnectedNotification();
        VerifyMessagePackSnapshot(original, "PeerDisconnectedNotification.msgpack");
    }
}
