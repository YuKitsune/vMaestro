using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class LandingStatisticsDtoSerializationTests
{
    [Fact]
    public void LandingStatisticsDto_Serialization_Json()
    {
        var original = CreateLandingStatisticsDto();
        VerifyJsonSnapshot(original, "LandingStatisticsDto.json");
    }

    [Fact]
    public void LandingStatisticsDto_Serialization_MessagePack()
    {
        var original = CreateLandingStatisticsDto();
        VerifyMessagePackSnapshot(original, "LandingStatisticsDto.msgpack");
    }
}
