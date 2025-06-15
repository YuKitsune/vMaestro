namespace Maestro.Core.Extensions;

public static class DateTimeOffsetHelpers
{
    public static DateTimeOffset Earliest(DateTimeOffset left, DateTimeOffset right)
    {
        return left.IsBefore(right) ? left : right;
    }
    
    public static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right)
    {
        return left.IsAfter(right) ? left : right;
    }
}