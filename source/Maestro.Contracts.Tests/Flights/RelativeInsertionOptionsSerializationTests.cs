using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class RelativeInsertionOptionsSerializationTests
{
    [Fact]
    public void RelativeInsertionOptions_Serialization_Json()
    {
        var original = CreateRelativeInsertionOptions();
        VerifyJsonSnapshot(original, "RelativeInsertionOptions.json");
    }

    [Fact]
    public void RelativeInsertionOptions_Serialization_MessagePack()
    {
        var original = CreateRelativeInsertionOptions();
        VerifyMessagePackSnapshot(original, "RelativeInsertionOptions.msgpack");
    }
}
