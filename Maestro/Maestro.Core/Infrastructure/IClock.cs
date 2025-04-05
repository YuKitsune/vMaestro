namespace Maestro.Core.Infrastructure;

public interface IClock
{
    DateTimeOffset ToUtc(DateTime dateTime);
    DateTimeOffset UtcNow();
}

public class SystemClock : IClock
{
    public DateTimeOffset ToUtc(DateTime dateTime) => dateTime.ToUniversalTime();
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}