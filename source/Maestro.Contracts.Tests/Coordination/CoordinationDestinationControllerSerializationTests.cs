using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Coordination;

public class CoordinationDestinationControllerSerializationTests
{
    [Fact]
    public void CoordinationDestination_Controller_Serialization_Json()
    {
        var original = CreateCoordinationDestinationController();
        VerifyJsonSnapshot(original, "CoordinationDestination_Controller.json");
    }

    [Fact]
    public void CoordinationDestination_Controller_Serialization_MessagePack()
    {
        var original = CreateCoordinationDestinationController();
        VerifyMessagePackSnapshot(original, "CoordinationDestination_Controller.msgpack");
    }
}
