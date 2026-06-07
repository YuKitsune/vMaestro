using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class CancelLandingRatesChangeRequestSerializationTests
{
    [Fact]
    public void CancelLandingRatesChangeRequest_Serialization_Json()
    {
        var original = CreateCancelLandingRatesChangeRequest();
        VerifyJsonSnapshot(original, "CancelLandingRatesChangeRequest.json");
    }

    [Fact]
    public void CancelLandingRatesChangeRequest_Serialization_MessagePack()
    {
        var original = CreateCancelLandingRatesChangeRequest();
        VerifyMessagePackSnapshot(original, "CancelLandingRatesChangeRequest.msgpack");
    }
}
