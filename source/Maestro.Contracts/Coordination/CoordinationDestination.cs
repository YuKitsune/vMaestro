using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Coordination;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(Broadcast), "Broadcast")]
[JsonDerivedType(typeof(Controller), "Controller")]

[MessagePackObject]
[Union(0, typeof(Broadcast))]
[Union(1, typeof(Controller))]
public abstract record CoordinationDestination
{
    private CoordinationDestination() { }

    [MessagePackObject]
    public sealed record Broadcast : CoordinationDestination;

    [MessagePackObject]
    public sealed record Controller(
        [property: Key(0)] string Callsign)
        : CoordinationDestination;
}
