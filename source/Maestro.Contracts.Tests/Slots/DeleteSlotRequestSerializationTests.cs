using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Slots;

public class DeleteSlotRequestSerializationTests
{
    [Fact]
    public void DeleteSlotRequest_Serialization_Json()
    {
        var original = CreateDeleteSlotRequest();
        VerifyJsonSnapshot(original, "DeleteSlotRequest.json");
    }

    [Fact]
    public void DeleteSlotRequest_Serialization_MessagePack()
    {
        var original = CreateDeleteSlotRequest();
        VerifyMessagePackSnapshot(original, "DeleteSlotRequest.msgpack");
    }
}
