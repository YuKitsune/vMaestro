using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class ServerResponseSerializationTests
{
    [Fact]
    public void ServerResponse_Serialization_Json()
    {
        var original = CreateServerResponse();
        VerifyJsonSnapshot(original, "ServerResponse.json");
    }

    [Fact]
    public void ServerResponse_Serialization_MessagePack()
    {
        var original = CreateServerResponse();
        VerifyMessagePackSnapshot(original, "ServerResponse.msgpack");
    }
}
