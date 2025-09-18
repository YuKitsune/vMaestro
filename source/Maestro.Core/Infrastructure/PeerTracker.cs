using Maestro.Core.Configuration;
using Maestro.Core.Messages.Connectivity;

namespace Maestro.Core.Infrastructure;

public interface IPeerTracker
{
    void AddPeer(string airportIdentifier, PeerInfo peer);
    void RemovePeer(string airportIdentifier, string callsign);
    PeerInfo[] GetPeers(string airportIdentifier);
}

public class PeerTracker : IPeerTracker
{
    readonly IDictionary<string, PeerInfo[]> _peers = new Dictionary<string, PeerInfo[]>();

    public void AddPeer(string airportIdentifier, PeerInfo peer)
    {
        if (_peers.TryGetValue(airportIdentifier, out var clients))
        {
            var updatedClients = clients.Append(peer).ToArray();
            _peers[airportIdentifier] = updatedClients;
        }
        else
        {
            _peers[airportIdentifier] = [peer];
        }
    }

    public void RemovePeer(string airportIdentifier, string callsign)
    {
        if (_peers.TryGetValue(airportIdentifier, out var clients))
        {
            var updatedClients = clients.Where(c => c.Callsign != callsign).ToArray();
            _peers[airportIdentifier] = updatedClients;
        }
    }

    public PeerInfo[] GetPeers(string airportIdentifier)
    {
        return _peers.TryGetValue(airportIdentifier, out var clients) ? clients : Array.Empty<PeerInfo>();
    }
}

public static class PeerTrackerExtensionMethods
{
    public static bool IsFlowControllerOnline(this IPeerTracker tracker, string airportIdentifier)
    {
        return tracker.GetPeers(airportIdentifier).Any(p => p.Role == Role.Flow);
    }
}
