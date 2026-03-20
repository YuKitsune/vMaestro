using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Slots;

public class CreateSlotRequestSerializationTests
{
    [Fact]
    public void CreateSlotRequest_Serialization_Json()
    {
        var original = CreateCreateSlotRequest();
        VerifyJsonSnapshot(original, "CreateSlotRequest.json");
    }

    [Fact]
    public void CreateSlotRequest_Serialization_MessagePack()
    {
        var original = CreateCreateSlotRequest();
        VerifyMessagePackSnapshot(original, "CreateSlotRequest.msgpack");
    }
}
