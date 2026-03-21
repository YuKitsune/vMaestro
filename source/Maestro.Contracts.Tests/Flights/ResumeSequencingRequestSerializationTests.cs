using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class ResumeSequencingRequestSerializationTests
{
    [Fact]
    public void ResumeSequencingRequest_Serialization_Json()
    {
        var original = CreateResumeSequencingRequest();
        VerifyJsonSnapshot(original, "ResumeSequencingRequest.json");
    }

    [Fact]
    public void ResumeSequencingRequest_Serialization_MessagePack()
    {
        var original = CreateResumeSequencingRequest();
        VerifyMessagePackSnapshot(original, "ResumeSequencingRequest.msgpack");
    }
}
