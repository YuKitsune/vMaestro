using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ExactInsertionOptionsSerializationTests
{
    [Fact]
    public void ExactInsertionOptions_Serialization_Json()
    {
        var original = CreateExactInsertionOptions();
        VerifyJsonSnapshot(original, "ExactInsertionOptions.json");
    }

    [Fact]
    public void ExactInsertionOptions_Serialization_MessagePack()
    {
        var original = CreateExactInsertionOptions();
        VerifyMessagePackSnapshot(original, "ExactInsertionOptions.msgpack");
    }
}
