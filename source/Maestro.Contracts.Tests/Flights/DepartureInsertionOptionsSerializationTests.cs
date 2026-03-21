using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class DepartureInsertionOptionsSerializationTests
{
    [Fact]
    public void DepartureInsertionOptions_Serialization_Json()
    {
        var original = CreateDepartureInsertionOptions();
        VerifyJsonSnapshot(original, "DepartureInsertionOptions.json");
    }

    [Fact]
    public void DepartureInsertionOptions_Serialization_MessagePack()
    {
        var original = CreateDepartureInsertionOptions();
        VerifyMessagePackSnapshot(original, "DepartureInsertionOptions.msgpack");
    }
}
