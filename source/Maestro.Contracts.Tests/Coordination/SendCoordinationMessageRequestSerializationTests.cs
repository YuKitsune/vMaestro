using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Coordination;

public class SendCoordinationMessageRequestSerializationTests
{
    [Fact]
    public void SendCoordinationMessageRequest_Serialization_Json()
    {
        var original = CreateSendCoordinationMessageRequest();
        VerifyJsonSnapshot(original, "SendCoordinationMessageRequest.json");
    }

    [Fact]
    public void SendCoordinationMessageRequest_Serialization_MessagePack()
    {
        var original = CreateSendCoordinationMessageRequest();
        VerifyMessagePackSnapshot(original, "SendCoordinationMessageRequest.msgpack");
    }
}
