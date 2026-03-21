using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class OwnershipRevokedNotificationSerializationTests
{
    [Fact]
    public void OwnershipRevokedNotification_Serialization_Json()
    {
        var original = CreateOwnershipRevokedNotification();
        VerifyJsonSnapshot(original, "OwnershipRevokedNotification.json");
    }

    [Fact]
    public void OwnershipRevokedNotification_Serialization_MessagePack()
    {
        var original = CreateOwnershipRevokedNotification();
        VerifyMessagePackSnapshot(original, "OwnershipRevokedNotification.msgpack");
    }
}
