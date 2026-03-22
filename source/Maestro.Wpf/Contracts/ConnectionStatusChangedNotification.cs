using Maestro.Contracts.Connectivity;

namespace Maestro.Wpf.Contracts;

public record ConnectionStatusChangedNotification(string AirportIdentifier, string Status, Role Role, bool FlowIsOnline);
