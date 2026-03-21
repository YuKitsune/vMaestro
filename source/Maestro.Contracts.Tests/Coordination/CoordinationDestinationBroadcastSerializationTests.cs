using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Coordination;

public class CoordinationDestinationBroadcastSerializationTests
{
    [Fact]
    public void CoordinationDestination_Broadcast_Serialization_Json()
    {
        var original = CreateCoordinationDestinationBroadcast();
        VerifyJsonSnapshot(original, "CoordinationDestination_Broadcast.json");
    }

    [Fact]
    public void CoordinationDestination_Broadcast_Serialization_MessagePack()
    {
        var original = CreateCoordinationDestinationBroadcast();
        VerifyMessagePackSnapshot(original, "CoordinationDestination_Broadcast.msgpack");
    }
}
