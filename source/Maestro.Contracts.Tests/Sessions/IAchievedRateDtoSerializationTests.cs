using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class IAchievedRateDtoSerializationTests
{
    [Fact]
    public void NoDeviationDto_Serialization_Json()
    {
        var original = CreateNoDeviationDto();
        VerifyJsonSnapshot(original, "NoDeviationDto.json");
    }

    [Fact]
    public void NoDeviationDto_Serialization_MessagePack()
    {
        var original = CreateNoDeviationDto();
        VerifyMessagePackSnapshot(original, "NoDeviationDto.msgpack");
    }

    [Fact]
    public void AchievedRateDto_Serialization_Json()
    {
        var original = CreateAchievedRateDto();
        VerifyJsonSnapshot(original, "AchievedRateDto.json");
    }

    [Fact]
    public void AchievedRateDto_Serialization_MessagePack()
    {
        var original = CreateAchievedRateDto();
        VerifyMessagePackSnapshot(original, "AchievedRateDto.msgpack");
    }
}
