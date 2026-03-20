using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class RemoveRequestSerializationTests
{
    [Fact]
    public void RemoveRequest_Serialization_Json()
    {
        var original = CreateRemoveRequest();
        VerifyJsonSnapshot(original, "RemoveRequest.json");
    }

    [Fact]
    public void RemoveRequest_Serialization_MessagePack()
    {
        var original = CreateRemoveRequest();
        VerifyMessagePackSnapshot(original, "RemoveRequest.msgpack");
    }
}
