namespace Maestro.Core.Infrastructure;

public interface IClock
{
    DateTimeOffset FromVatSysTime(DateTime dateTime);
    DateTimeOffset UtcNow();
}

public class SystemClock : IClock
{
    public DateTimeOffset FromVatSysTime(DateTime dateTime) => new(
        dateTime.Year, dateTime.Month, dateTime.Day,
        dateTime.Hour, dateTime.Minute, dateTime.Second,
        TimeSpan.Zero);
    
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}