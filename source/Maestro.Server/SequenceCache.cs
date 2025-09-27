using Maestro.Core.Messages;

namespace Maestro.Server;

public class SequenceCache
{
    readonly IDictionary<string, SequenceMessage> _sequence = new Dictionary<string, SequenceMessage>();

    public SequenceMessage? Get(
        string partition,
        string airportIdentifier)
    {
        var key = CreateKey(partition, airportIdentifier);
        return !_sequence.TryGetValue(key, out var sequenceMessage) ? null : sequenceMessage;
    }

    public void Set(string partition, string airportIdentifier, SequenceMessage sequenceMessage)
    {
        var key = CreateKey(partition, airportIdentifier);
        _sequence[key] = sequenceMessage;
    }

    public void Evict(string partition, string airportIdentifier)
    {
        var key = CreateKey(partition, airportIdentifier);
        _sequence.Remove(key);
    }

    string CreateKey(string partition, string airportIdentifier)
    {
        return $"{partition}:{airportIdentifier}";
    }
}
