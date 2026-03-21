using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class RestoreSessionRequestSerializationTests
{
    [Fact]
    public void RestoreSessionRequest_Serialization_Json()
    {
        var original = CreateRestoreSessionRequest();
        VerifyJsonSnapshot(original, "RestoreSessionRequest.json");
    }

    [Fact]
    public void RestoreSessionRequest_Serialization_MessagePack()
    {
        var original = CreateRestoreSessionRequest();
        VerifyMessagePackSnapshot(original, "RestoreSessionRequest.msgpack");
    }
}
