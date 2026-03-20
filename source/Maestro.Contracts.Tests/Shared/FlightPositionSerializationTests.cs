using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Shared;

public class FlightPositionSerializationTests
{
    [Fact]
    public void FlightPosition_Serialization_Json()
    {
        var original = CreateFlightPosition();
        VerifyJsonSnapshot(original, "FlightPosition.json");
    }

    [Fact]
    public void FlightPosition_Serialization_MessagePack()
    {
        var original = CreateFlightPosition();
        VerifyMessagePackSnapshot(original, "FlightPosition.msgpack");
    }
}
