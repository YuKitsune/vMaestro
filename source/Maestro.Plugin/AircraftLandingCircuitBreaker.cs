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

    /// <summary>
    /// Resets the breaker for the given callsign, allowing it to be tripped again.
    /// </summary>
    public void ResetBreaker(string callsign)
    {
        lock (_gate)
        {
            _breakers.Remove(callsign);
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
