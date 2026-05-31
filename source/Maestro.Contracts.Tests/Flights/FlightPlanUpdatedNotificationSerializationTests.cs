using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class FlightPlanUpdatedNotificationSerializationTests
{
    [Fact]
    public void FlightPlanUpdatedNotification_Serialization_Json()
    {
        var original = CreateFlightPlanUpdatedNotification();
        VerifyJsonSnapshot(original, "FlightPlanUpdatedNotification.json");
    }

    [Fact]
    public void FlightPlanUpdatedNotification_Serialization_MessagePack()
    {
        var original = CreateFlightPlanUpdatedNotification();
        VerifyMessagePackSnapshot(original, "FlightPlanUpdatedNotification.msgpack");
    }
}
