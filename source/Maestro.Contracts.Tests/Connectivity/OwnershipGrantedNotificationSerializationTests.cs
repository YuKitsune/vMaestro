using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class OwnershipGrantedNotificationSerializationTests
{
    [Fact]
    public void OwnershipGrantedNotification_Serialization_Json()
    {
        var original = CreateOwnershipGrantedNotification();
        VerifyJsonSnapshot(original, "OwnershipGrantedNotification.json");
    }

    [Fact]
    public void OwnershipGrantedNotification_Serialization_MessagePack()
    {
        var original = CreateOwnershipGrantedNotification();
        VerifyMessagePackSnapshot(original, "OwnershipGrantedNotification.msgpack");
    }
}
