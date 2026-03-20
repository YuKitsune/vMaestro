using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Slots;

public class ModifySlotRequestSerializationTests
{
    [Fact]
    public void ModifySlotRequest_Serialization_Json()
    {
        var original = CreateModifySlotRequest();
        VerifyJsonSnapshot(original, "ModifySlotRequest.json");
    }

    [Fact]
    public void ModifySlotRequest_Serialization_MessagePack()
    {
        var original = CreateModifySlotRequest();
        VerifyMessagePackSnapshot(original, "ModifySlotRequest.msgpack");
    }
}
