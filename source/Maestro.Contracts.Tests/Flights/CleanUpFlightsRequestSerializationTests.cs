using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class CleanUpFlightsRequestSerializationTests
{
    [Fact]
    public void CleanUpFlightsRequest_Serialization_Json()
    {
        var original = CreateCleanUpFlightsRequest();
        VerifyJsonSnapshot(original, "CleanUpFlightsRequest.json");
    }

    [Fact]
    public void CleanUpFlightsRequest_Serialization_MessagePack()
    {
        var original = CreateCleanUpFlightsRequest();
        VerifyMessagePackSnapshot(original, "CleanUpFlightsRequest.msgpack");
    }
}
