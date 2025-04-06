using Maestro.Core.Infrastructure;

namespace Maestro.Core.Tests;

public class FixedClock(DateTimeOffset now) : IClock
{
    DateTimeOffset _now = now;
    public DateTimeOffset UtcNow() => _now;
    public void SetTime(DateTimeOffset now) => _now = now;
}