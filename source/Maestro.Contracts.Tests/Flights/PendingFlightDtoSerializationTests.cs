using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class PendingFlightDtoSerializationTests
{
    [Fact]
    public void PendingFlightDto_Serialization_Json()
    {
        var original = CreatePendingFlightDto();
        VerifyJsonSnapshot(original, "PendingFlightDto.json");
    }

    [Fact]
    public void PendingFlightDto_Serialization_MessagePack()
    {
        var original = CreatePendingFlightDto();
        VerifyMessagePackSnapshot(original, "PendingFlightDto.msgpack");
    }
}
