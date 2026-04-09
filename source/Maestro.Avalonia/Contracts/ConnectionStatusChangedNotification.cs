using Maestro.Contracts.Connectivity;

namespace Maestro.Avalonia.Contracts;

public record ConnectionStatusChangedNotification(string AirportIdentifier, string Status, Role Role, bool FlowIsOnline);
