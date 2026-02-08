namespace ParkingApp.BuildingBlocks.Extensions;

/// <summary>
/// Extension methods for strings
/// </summary>
public static class StringExtensions
{
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);
    
    public static bool IsNullOrWhiteSpace(this string? value) => string.IsNullOrWhiteSpace(value);
    
    public static string? ToNullIfEmpty(this string? value) => 
        string.IsNullOrEmpty(value) ? null : value;
    
    public static string? ToNullIfWhiteSpace(this string? value) => 
        string.IsNullOrWhiteSpace(value) ? null : value;
    
    public static string Truncate(this string value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        
        return value[..(maxLength - suffix.Length)] + suffix;
    }
    
    public static string ToTitleCase(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLower());
    }
    
    public static string RemoveWhitespace(this string value) =>
        new(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
}

/// <summary>
/// Extension methods for DateTime
/// </summary>
public static class DateTimeExtensions
{
    public static DateTime StartOfDay(this DateTime date) => date.Date;
    
    public static DateTime EndOfDay(this DateTime date) => 
        date.Date.AddDays(1).AddTicks(-1);
    
    public static DateTime StartOfMonth(this DateTime date) => 
        new(date.Year, date.Month, 1);
    
    public static DateTime EndOfMonth(this DateTime date) => 
        new DateTime(date.Year, date.Month, 1).AddMonths(1).AddTicks(-1);
    
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek = DayOfWeek.Monday)
    {
        int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
    
    public static bool IsBetween(this DateTime date, DateTime start, DateTime end) =>
        date >= start && date <= end;
    
    public static string ToRelativeTime(this DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;
        
        return timeSpan.TotalMinutes switch
        {
            < 1 => "just now",
            < 60 => $"{(int)timeSpan.TotalMinutes}m ago",
            < 1440 => $"{(int)timeSpan.TotalHours}h ago",
            < 43200 => $"{(int)timeSpan.TotalDays}d ago",
            _ => dateTime.ToString("MMM dd, yyyy")
        };
    }
}

/// <summary>
/// Extension methods for collections
/// </summary>
public static class CollectionExtensions
{
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? collection) =>
        collection is null || !collection.Any();
    
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class =>
        source.Where(item => item != null)!;
    
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var list = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }
        return list;
    }
    
    public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        using var enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return GetBatch(enumerator, batchSize);
        }
    }
    
    private static IEnumerable<T> GetBatch<T>(IEnumerator<T> enumerator, int batchSize)
    {
        yield return enumerator.Current;
        for (int i = 1; i < batchSize && enumerator.MoveNext(); i++)
        {
            yield return enumerator.Current;
        }
    }
}
