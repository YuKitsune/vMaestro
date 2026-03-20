using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class FlightUpdatedNotificationSerializationTests
{
    [Fact]
    public void FlightUpdatedNotification_Serialization_Json()
    {
        var original = CreateFlightUpdatedNotification();
        VerifyJsonSnapshot(original, "FlightUpdatedNotification.json");
    }

    [Fact]
    public void FlightUpdatedNotification_Serialization_MessagePack()
    {
        var original = CreateFlightUpdatedNotification();
        VerifyMessagePackSnapshot(original, "FlightUpdatedNotification.msgpack");
    }
}
