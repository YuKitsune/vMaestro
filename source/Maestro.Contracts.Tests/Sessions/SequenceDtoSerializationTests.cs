using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class SequenceDtoSerializationTests
{
    [Fact]
    public void SequenceDto_Serialization_Json()
    {
        var original = CreateSequenceDto();
        VerifyJsonSnapshot(original, "SequenceDto.json");
    }

    [Fact]
    public void SequenceDto_Serialization_MessagePack()
    {
        var original = CreateSequenceDto();
        VerifyMessagePackSnapshot(original, "SequenceDto.msgpack");
    }
}
