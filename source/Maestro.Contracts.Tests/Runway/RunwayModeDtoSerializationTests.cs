using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class RunwayModeDtoSerializationTests
{
    [Fact]
    public void RunwayModeDto_Serialization_Json()
    {
        var original = CreateRunwayModeDto();
        VerifyJsonSnapshot(original, "RunwayModeDto.json");
    }

    [Fact]
    public void RunwayModeDto_Serialization_MessagePack()
    {
        var original = CreateRunwayModeDto();
        VerifyMessagePackSnapshot(original, "RunwayModeDto.msgpack");
    }
}
