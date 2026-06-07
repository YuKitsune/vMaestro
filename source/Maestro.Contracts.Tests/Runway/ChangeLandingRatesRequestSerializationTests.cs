using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class ChangeLandingRatesRequestSerializationTests
{
    [Fact]
    public void ChangeLandingRatesRequest_Serialization_Json()
    {
        var original = CreateChangeLandingRatesRequest();
        VerifyJsonSnapshot(original, "ChangeLandingRatesRequest.json");
    }

    [Fact]
    public void ChangeLandingRatesRequest_Serialization_MessagePack()
    {
        var original = CreateChangeLandingRatesRequest();
        VerifyMessagePackSnapshot(original, "ChangeLandingRatesRequest.msgpack");
    }
}
