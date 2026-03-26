using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class FlightDataRecordSerializationTests
{
    [Fact]
    public void FlightDataRecord_Serialization_Json()
    {
        var original = CreateFlightDataRecord();
        VerifyJsonSnapshot(original, "FlightDataRecord.json");
    }

    [Fact]
    public void FlightDataRecord_Serialization_MessagePack()
    {
        var original = CreateFlightDataRecord();
        VerifyMessagePackSnapshot(original, "FlightDataRecord.msgpack");
    }
}
