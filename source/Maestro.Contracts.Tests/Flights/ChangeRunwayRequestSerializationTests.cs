using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ChangeRunwayRequestSerializationTests
{
    [Fact]
    public void ChangeRunwayRequest_Serialization_Json()
    {
        var original = CreateChangeRunwayRequest();
        VerifyJsonSnapshot(original, "ChangeRunwayRequest.json");
    }

    [Fact]
    public void ChangeRunwayRequest_Serialization_MessagePack()
    {
        var original = CreateChangeRunwayRequest();
        VerifyMessagePackSnapshot(original, "ChangeRunwayRequest.msgpack");
    }
}
