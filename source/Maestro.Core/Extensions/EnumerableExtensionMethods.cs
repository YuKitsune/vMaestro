namespace Maestro.Core.Extensions;

public static class EnumerableExtensionMethods
{
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class
    {
        return source.Where(item => item is not null)!;
    }
}
