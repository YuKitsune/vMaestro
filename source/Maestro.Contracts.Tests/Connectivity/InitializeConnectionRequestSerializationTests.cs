using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class InitializeConnectionRequestSerializationTests
{
    [Fact]
    public void InitializeConnectionRequest_Serialization_Json()
    {
        var original = CreateInitializeConnectionRequest();
        VerifyJsonSnapshot(original, "InitializeConnectionRequest.json");
    }

    [Fact]
    public void InitializeConnectionRequest_Serialization_MessagePack()
    {
        var original = CreateInitializeConnectionRequest();
        VerifyMessagePackSnapshot(original, "InitializeConnectionRequest.msgpack");
    }
}
