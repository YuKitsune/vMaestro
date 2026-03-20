using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class SwapFlightsRequestSerializationTests
{
    [Fact]
    public void SwapFlightsRequest_Serialization_Json()
    {
        var original = CreateSwapFlightsRequest();
        VerifyJsonSnapshot(original, "SwapFlightsRequest.json");
    }

    [Fact]
    public void SwapFlightsRequest_Serialization_MessagePack()
    {
        var original = CreateSwapFlightsRequest();
        VerifyMessagePackSnapshot(original, "SwapFlightsRequest.msgpack");
    }
}
