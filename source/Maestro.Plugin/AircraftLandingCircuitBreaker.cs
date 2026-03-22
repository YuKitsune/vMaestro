namespace Maestro.Plugin;

public class AircraftLandingCircuitBreaker
{
    readonly object _gate = new();
    readonly Dictionary<string, CircuitBreaker> _breakers = new();

    /// <summary>
    /// Returns <c>true</c> if the breaker was tripped as part of this call,
    /// <c>false</c> if had previously been tripped
    /// </summary>
    public bool TrySetBreaker(string callsign)
    {
        lock (_gate)
        {
            if (!_breakers.TryGetValue(callsign, out var breaker))
            {
                _breakers[callsign] = breaker = new CircuitBreaker();
            }

            return breaker.TrySet();
        }
    }

    class CircuitBreaker
    {
        public bool IsSet { get; private set; }

        public bool TrySet()
        {
            if (IsSet)
                return false;

            IsSet = true;
            return true;
        }
    }
}
