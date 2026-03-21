using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Sessions;

public class SessionDtoSerializationTests
{
    [Fact]
    public void SessionDto_Serialization_Json()
    {
        var original = CreateSessionDto();
        VerifyJsonSnapshot(original, "SessionDto.json");
    }

    [Fact]
    public void SessionDto_Serialization_MessagePack()
    {
        var original = CreateSessionDto();
        VerifyMessagePackSnapshot(original, "SessionDto.msgpack");
    }
}
