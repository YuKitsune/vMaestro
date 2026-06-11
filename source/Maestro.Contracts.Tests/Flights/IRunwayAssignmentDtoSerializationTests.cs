using System.Text.Json;
using Maestro.Contracts.Flights;
using MessagePack;
using Shouldly;
using static Maestro.Contracts.Tests.SnapshotTestHelper;
using static Maestro.Contracts.Tests.TestBuilders;

namespace Maestro.Contracts.Tests.Flights;

public class IRunwayAssignmentDtoSerializationTests
{
    [Fact]
    public void AutomaticRunwayAssignmentDto_Serialization_Json()
    {
        var original = CreateAutomaticRunwayAssignmentDto();
        VerifyJsonSnapshot(original, "AutomaticRunwayAssignmentDto.json");
    }

    [Fact]
    public void AutomaticRunwayAssignmentDto_Serialization_MessagePack()
    {
        var original = CreateAutomaticRunwayAssignmentDto();
        VerifyMessagePackSnapshot(original, "AutomaticRunwayAssignmentDto.msgpack");
    }

    [Fact]
    public void ManualRunwayAssignmentDto_Serialization_Json()
    {
        var original = CreateManualRunwayAssignmentDto();
        VerifyJsonSnapshot(original, "ManualRunwayAssignmentDto.json");
    }

    [Fact]
    public void ManualRunwayAssignmentDto_Serialization_MessagePack()
    {
        var original = CreateManualRunwayAssignmentDto();
        VerifyMessagePackSnapshot(original, "ManualRunwayAssignmentDto.msgpack");
    }

    [Fact]
    public void AutomaticRunwayAssignmentDto_RoundTripsThroughInterface()
        => AssertRoundTripsThroughInterface(CreateAutomaticRunwayAssignmentDto());

    [Fact]
    public void ManualRunwayAssignmentDto_RoundTripsThroughInterface()
        => AssertRoundTripsThroughInterface(CreateManualRunwayAssignmentDto());

    // Serializing through the interface must preserve the concrete assignment type (the union
    // discriminator) so a manual assignment is never silently downgraded to automatic on the wire.
    static void AssertRoundTripsThroughInterface(IRunwayAssignmentDto original)
    {
        var json = JsonSerializer.Serialize(original, JsonOptions);
        var fromJson = JsonSerializer.Deserialize<IRunwayAssignmentDto>(json, JsonOptions);
        fromJson.ShouldNotBeNull();
        fromJson.ShouldBeOfType(original.GetType());
        fromJson.RunwayIdentifier.ShouldBe(original.RunwayIdentifier);

        var bytes = MessagePackSerializer.Serialize(original, MessagePackOptions);
        var fromMessagePack = MessagePackSerializer.Deserialize<IRunwayAssignmentDto>(bytes, MessagePackOptions);
        fromMessagePack.ShouldNotBeNull();
        fromMessagePack.ShouldBeOfType(original.GetType());
        fromMessagePack.RunwayIdentifier.ShouldBe(original.RunwayIdentifier);
    }
}
