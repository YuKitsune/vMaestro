using System.Text.Json.Serialization;
using MessagePack;

namespace Maestro.Contracts.Coordination;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(Broadcast), "Broadcast")]
[JsonDerivedType(typeof(Controller), "Controller")]
[Union(0, typeof(Broadcast))]
[Union(1, typeof(Controller))]
public abstract record CoordinationDestination
{
    private CoordinationDestination() { }

    public sealed record Broadcast : CoordinationDestination;

    public sealed record Controller(string Callsign) : CoordinationDestination;
}
