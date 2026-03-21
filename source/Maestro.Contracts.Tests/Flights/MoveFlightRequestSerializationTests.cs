using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class MoveFlightRequestSerializationTests
{
    [Fact]
    public void MoveFlightRequest_Serialization_Json()
    {
        var original = CreateMoveFlightRequest();
        VerifyJsonSnapshot(original, "MoveFlightRequest.json");
    }

    [Fact]
    public void MoveFlightRequest_Serialization_MessagePack()
    {
        var original = CreateMoveFlightRequest();
        VerifyMessagePackSnapshot(original, "MoveFlightRequest.msgpack");
    }
}
