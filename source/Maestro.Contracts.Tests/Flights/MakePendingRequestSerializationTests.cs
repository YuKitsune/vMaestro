using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class MakePendingRequestSerializationTests
{
    [Fact]
    public void MakePendingRequest_Serialization_Json()
    {
        var original = CreateMakePendingRequest();
        VerifyJsonSnapshot(original, "MakePendingRequest.json");
    }

    [Fact]
    public void MakePendingRequest_Serialization_MessagePack()
    {
        var original = CreateMakePendingRequest();
        VerifyMessagePackSnapshot(original, "MakePendingRequest.msgpack");
    }
}
