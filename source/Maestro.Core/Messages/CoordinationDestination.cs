namespace Maestro.Core.Messages;

public abstract record CoordinationDestination
{
    private CoordinationDestination() { }

    public sealed record Broadcast : CoordinationDestination;

    public sealed record Controller(string Callsign) : CoordinationDestination;
}
