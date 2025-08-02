using Maestro.Core.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(ClockFixture))]

namespace Maestro.Core.Tests.Fixtures;
public class ClockFixture
{
    public FixedClock Instance => new(
        new DateTimeOffset(2025, 01, 01, 0, 0, 0, TimeSpan.Zero));
}
