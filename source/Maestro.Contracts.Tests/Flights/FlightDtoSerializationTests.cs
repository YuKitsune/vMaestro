using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class FlightDtoSerializationTests
{
    [Fact]
    public void FlightDto_Serialization_Json()
    {
        var original = CreateFlightDto();
        VerifyJsonSnapshot(original, "FlightDto.json");
    }

    [Fact]
    public void FlightDto_Serialization_MessagePack()
    {
        var original = CreateFlightDto();
        VerifyMessagePackSnapshot(original, "FlightDto.msgpack");
    }
}
