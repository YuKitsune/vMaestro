using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class CancelRunwayModeChangeRequestSerializationTests
{
    [Fact]
    public void CancelRunwayModeChangeRequest_Serialization_Json()
    {
        var original = CreateCancelRunwayModeChangeRequest();
        VerifyJsonSnapshot(original, "CancelRunwayModeChangeRequest.json");
    }

    [Fact]
    public void CancelRunwayModeChangeRequest_Serialization_MessagePack()
    {
        var original = CreateCancelRunwayModeChangeRequest();
        VerifyMessagePackSnapshot(original, "CancelRunwayModeChangeRequest.msgpack");
    }
}
