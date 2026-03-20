using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class PeerConnectedNotificationSerializationTests
{
    [Fact]
    public void PeerConnectedNotification_Serialization_Json()
    {
        var original = CreatePeerConnectedNotification();
        VerifyJsonSnapshot(original, "PeerConnectedNotification.json");
    }

    [Fact]
    public void PeerConnectedNotification_Serialization_MessagePack()
    {
        var original = CreatePeerConnectedNotification();
        VerifyMessagePackSnapshot(original, "PeerConnectedNotification.msgpack");
    }
}
