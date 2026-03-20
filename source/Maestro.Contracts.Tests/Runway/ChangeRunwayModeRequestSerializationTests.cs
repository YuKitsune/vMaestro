using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class ChangeRunwayModeRequestSerializationTests
{
    [Fact]
    public void ChangeRunwayModeRequest_Serialization_Json()
    {
        var original = CreateChangeRunwayModeRequest();
        VerifyJsonSnapshot(original, "ChangeRunwayModeRequest.json");
    }

    [Fact]
    public void ChangeRunwayModeRequest_Serialization_MessagePack()
    {
        var original = CreateChangeRunwayModeRequest();
        VerifyMessagePackSnapshot(original, "ChangeRunwayModeRequest.msgpack");
    }
}
