using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class MakeStableRequestSerializationTests
{
    [Fact]
    public void MakeStableRequest_Serialization_Json()
    {
        var original = CreateMakeStableRequest();
        VerifyJsonSnapshot(original, "MakeStableRequest.json");
    }

    [Fact]
    public void MakeStableRequest_Serialization_MessagePack()
    {
        var original = CreateMakeStableRequest();
        VerifyMessagePackSnapshot(original, "MakeStableRequest.msgpack");
    }
}
