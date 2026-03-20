using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class SessionUpdatedNotificationSerializationTests
{
    [Fact]
    public void SessionUpdatedNotification_Serialization_Json()
    {
        var original = CreateSessionUpdatedNotification();
        VerifyJsonSnapshot(original, "SessionUpdatedNotification.json");
    }

    [Fact]
    public void SessionUpdatedNotification_Serialization_MessagePack()
    {
        var original = CreateSessionUpdatedNotification();
        VerifyMessagePackSnapshot(original, "SessionUpdatedNotification.msgpack");
    }
}
