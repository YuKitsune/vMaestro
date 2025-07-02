namespace Maestro.Core.Extensions;

public static class DateTimeOffsetExtensionMethods
{
    public static bool IsBefore(this DateTimeOffset left, DateTimeOffset right)
    {
        return left < right;
    }
    public static bool IsSameOrBefore(this DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right;
    }

    public static bool IsAfter(this DateTimeOffset left, DateTimeOffset right)
    {
        return left > right;
    }

    public static bool IsSameOrAfter(this DateTimeOffset left, DateTimeOffset right)
    {
        return left >= right;
    }
}