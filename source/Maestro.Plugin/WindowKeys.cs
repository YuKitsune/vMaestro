namespace Maestro.Plugin;

public static class WindowKeys
{
    public static string SetupWindow => "setup";
    public static string Maestro(string airportIdentifier) => $"tfms-{airportIdentifier}";
    public static string Information(string callsign) => $"information-{callsign}";
    public static string ChangeFeederFixEstimate(string airportIdentifier) => $"change-eta-ff-{airportIdentifier}";
    public static string InsertFlight(string airportIdentifier) => $"insert-flight-{airportIdentifier}";
    public static string InsertDeparture(string airportIdentifier) => $"insert-departure-{airportIdentifier}";
    public static string Desequenced(string airportIdentifier) => $"desequenced-{airportIdentifier}";
    public static string Slot(string airportIdentifier) => $"slot-{airportIdentifier}";
    public static string TerminalConfiguration(string airportIdentifier) => $"terminal-configuration-{airportIdentifier}";
    public static string Information2(string callsign) => $"information2-{callsign}";
}
