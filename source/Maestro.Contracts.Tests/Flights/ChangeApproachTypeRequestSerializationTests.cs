using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ChangeApproachTypeRequestSerializationTests
{
    [Fact]
    public void ChangeApproachTypeRequest_Serialization_Json()
    {
        var original = CreateChangeApproachTypeRequest();
        VerifyJsonSnapshot(original, "ChangeApproachTypeRequest.json");
    }

    [Fact]
    public void ChangeApproachTypeRequest_Serialization_MessagePack()
    {
        var original = CreateChangeApproachTypeRequest();
        VerifyMessagePackSnapshot(original, "ChangeApproachTypeRequest.msgpack");
    }
}
