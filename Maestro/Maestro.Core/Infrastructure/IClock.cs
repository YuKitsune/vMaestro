namespace Maestro.Core.Infrastructure;

public interface IClock
{
    DateTimeOffset UtcNow();
}

public class SystemClock : IClock
{
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}