namespace ParkingApp.BuildingBlocks.Common;

/// <summary>
/// Guard clauses for argument validation
/// </summary>
public static class Guard
{
    public static T AgainstNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, $"{paramName} cannot be null");
        return value;
    }
    
    public static string AgainstNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        return value;
    }
    
    public static string AgainstNullOrWhiteSpace(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be null or whitespace", paramName);
        return value;
    }
    
    public static Guid AgainstEmpty(Guid value, string paramName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{paramName} cannot be an empty GUID", paramName);
        return value;
    }
    
    public static T AgainstDefault<T>(T value, string paramName) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            throw new ArgumentException($"{paramName} cannot be the default value", paramName);
        return value;
    }
    
    public static int AgainstNegative(int value, string paramName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be negative");
        return value;
    }
    
    public static decimal AgainstNegative(decimal value, string paramName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be negative");
        return value;
    }
    
    public static int AgainstNegativeOrZero(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be greater than zero");
        return value;
    }
    
    public static decimal AgainstNegativeOrZero(decimal value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be greater than zero");
        return value;
    }
    
    public static T AgainstOutOfRange<T>(T value, string paramName, T min, T max) where T : IComparable<T>
    {
        if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be between {min} and {max}");
        return value;
    }
    
    public static IEnumerable<T> AgainstNullOrEmpty<T>(IEnumerable<T>? collection, string paramName)
    {
        if (collection is null || !collection.Any())
            throw new ArgumentException($"{paramName} cannot be null or empty", paramName);
        return collection;
    }
    
    public static string AgainstInvalidEmail(string? email, string paramName)
    {
        AgainstNullOrWhiteSpace(email, paramName);
        if (!email!.Contains('@') || !email.Contains('.'))
            throw new ArgumentException($"{paramName} is not a valid email address", paramName);
        return email;
    }
    
    public static DateTime AgainstPast(DateTime value, string paramName)
    {
        if (value < DateTime.UtcNow)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be in the past");
        return value;
    }
    
    public static DateTime AgainstFuture(DateTime value, string paramName)
    {
        if (value > DateTime.UtcNow)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} cannot be in the future");
        return value;
    }
}
