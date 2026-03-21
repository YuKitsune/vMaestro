using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class InitializeConnectionResponseSerializationTests
{
    [Fact]
    public void InitializeConnectionResponse_Serialization_Json()
    {
        var original = CreateInitializeConnectionResponse();
        VerifyJsonSnapshot(original, "InitializeConnectionResponse.json");
    }

    [Fact]
    public void InitializeConnectionResponse_Serialization_MessagePack()
    {
        var original = CreateInitializeConnectionResponse();
        VerifyMessagePackSnapshot(original, "InitializeConnectionResponse.msgpack");
    }
}
