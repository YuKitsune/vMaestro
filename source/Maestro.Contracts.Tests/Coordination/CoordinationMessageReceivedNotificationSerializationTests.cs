using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Coordination;

public class CoordinationMessageReceivedNotificationSerializationTests
{
    [Fact]
    public void CoordinationMessageReceivedNotification_Serialization_Json()
    {
        var original = CreateCoordinationMessageReceivedNotification();
        VerifyJsonSnapshot(original, "CoordinationMessageReceivedNotification.json");
    }

    [Fact]
    public void CoordinationMessageReceivedNotification_Serialization_MessagePack()
    {
        var original = CreateCoordinationMessageReceivedNotification();
        VerifyMessagePackSnapshot(original, "CoordinationMessageReceivedNotification.msgpack");
    }
}
