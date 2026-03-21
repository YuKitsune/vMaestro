using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class UpdateWindRequestSerializationTests
{
    [Fact]
    public void UpdateWindRequest_Serialization_Json()
    {
        var original = CreateUpdateWindRequest();
        VerifyJsonSnapshot(original, "UpdateWindRequest.json");
    }

    [Fact]
    public void UpdateWindRequest_Serialization_MessagePack()
    {
        var original = CreateUpdateWindRequest();
        VerifyMessagePackSnapshot(original, "UpdateWindRequest.msgpack");
    }
}
