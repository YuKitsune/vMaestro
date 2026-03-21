using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Slots;

public class SlotDtoSerializationTests
{
    [Fact]
    public void SlotDto_Serialization_Json()
    {
        var original = CreateSlotDto();
        VerifyJsonSnapshot(original, "SlotDto.json");
    }

    [Fact]
    public void SlotDto_Serialization_MessagePack()
    {
        var original = CreateSlotDto();
        VerifyMessagePackSnapshot(original, "SlotDto.msgpack");
    }
}
