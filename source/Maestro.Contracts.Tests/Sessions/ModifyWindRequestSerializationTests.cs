using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class ModifyWindRequestSerializationTests
{
    [Fact]
    public void ModifyWindRequest_Serialization_Json()
    {
        var original = CreateModifyWindRequest();
        VerifyJsonSnapshot(original, "ModifyWindRequest.json");
    }

    [Fact]
    public void ModifyWindRequest_Serialization_MessagePack()
    {
        var original = CreateModifyWindRequest();
        VerifyMessagePackSnapshot(original, "ModifyWindRequest.msgpack");
    }
}
