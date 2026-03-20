using System.Text.Json.Serialization;

namespace Maestro.Contracts.Coordination;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "Type")]
[JsonDerivedType(typeof(Broadcast), "Broadcast")]
[JsonDerivedType(typeof(Controller), "Controller")]
public abstract record CoordinationDestination
{
    private CoordinationDestination() { }

    public sealed record Broadcast : CoordinationDestination;

    public sealed record Controller(string Callsign) : CoordinationDestination;
}
