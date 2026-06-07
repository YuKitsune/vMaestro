using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Runway;

public class CancelConfigurationChangeRequestSerializationTests
{
    [Fact]
    public void CancelConfigurationChangeRequest_Serialization_Json()
    {
        var original = CreateCancelConfigurationChangeRequest();
        VerifyJsonSnapshot(original, "CancelConfigurationChangeRequest.json");
    }

    [Fact]
    public void CancelConfigurationChangeRequest_Serialization_MessagePack()
    {
        var original = CreateCancelConfigurationChangeRequest();
        VerifyMessagePackSnapshot(original, "CancelConfigurationChangeRequest.msgpack");
    }
}
