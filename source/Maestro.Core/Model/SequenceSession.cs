namespace Maestro.Core.Model;

public class SequenceSession
{
    public string AirportIdentifier { get; set; }
    public string PositionName { get; set; }
    public Role Role { get; set; }
}

public enum Role
{
    Flow,
    Enroute,
    Approach,
    ReadOnly
}
