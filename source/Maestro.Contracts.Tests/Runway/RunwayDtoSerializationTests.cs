using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class RunwayDtoSerializationTests
{
    [Fact]
    public void RunwayDto_Serialization_Json()
    {
        var original = CreateRunwayDto();
        VerifyJsonSnapshot(original, "RunwayDto.json");
    }

    [Fact]
    public void RunwayDto_Serialization_MessagePack()
    {
        var original = CreateRunwayDto();
        VerifyMessagePackSnapshot(original, "RunwayDto.msgpack");
    }
}
