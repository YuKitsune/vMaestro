using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Shared;

public class CoordinateSerializationTests
{
    [Fact]
    public void Coordinate_Serialization_Json()
    {
        var original = CreateCoordinate();
        VerifyJsonSnapshot(original, "Coordinate.json");
    }

    [Fact]
    public void Coordinate_Serialization_MessagePack()
    {
        var original = CreateCoordinate();
        VerifyMessagePackSnapshot(original, "Coordinate.msgpack");
    }
}
