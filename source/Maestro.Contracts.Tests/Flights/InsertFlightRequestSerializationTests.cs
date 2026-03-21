using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class InsertFlightRequestSerializationTests
{
    [Fact]
    public void InsertFlightRequest_Serialization_Json()
    {
        var original = CreateInsertFlightRequest();
        VerifyJsonSnapshot(original, "InsertFlightRequest.json");
    }

    [Fact]
    public void InsertFlightRequest_Serialization_MessagePack()
    {
        var original = CreateInsertFlightRequest();
        VerifyMessagePackSnapshot(original, "InsertFlightRequest.msgpack");
    }
}
