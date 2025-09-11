namespace Maestro.Server;

public record struct GroupKey(string Partition, string AirportIdentifier)
{
    public string Value => $"{Partition}/{AirportIdentifier}";
    public override string ToString() => Value;
}
