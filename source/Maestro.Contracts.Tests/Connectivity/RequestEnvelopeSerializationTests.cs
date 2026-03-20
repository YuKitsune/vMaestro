using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Connectivity;

public class RequestEnvelopeSerializationTests
{
    [Fact]
    public void RequestEnvelope_Serialization_Json()
    {
        var original = CreateRequestEnvelope();
        VerifyJsonSnapshot(original, "RequestEnvelope.json");
    }

    [Fact]
    public void RequestEnvelope_Serialization_MessagePack()
    {
        var original = CreateRequestEnvelope();
        VerifyMessagePackSnapshot(original, "RequestEnvelope.msgpack");
    }
}
