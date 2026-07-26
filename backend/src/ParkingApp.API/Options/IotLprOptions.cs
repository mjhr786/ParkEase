namespace ParkingApp.API.Options;

public sealed class IotLprOptions
{
    public const string SectionName = "Iot:Lpr";

    public List<IotLprApiKeyOptions> ApiKeys { get; set; } = new();
    public int MaxOccurredAtSkewMinutes { get; set; } = 5;
    public int MaxOccurredAtAgeHours { get; set; } = 24;

    /// <summary>Max IoT LPR requests per client IP per minute (stricter than global API limit).</summary>
    public int RateLimitPerMinute { get; set; } = 30;
}

public sealed class IotLprApiKeyOptions
{
    public string KeyId { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Facility GUIDs this key may access. Empty/null = unrestricted (typical for local dev).
    /// </summary>
    public List<string> AllowedParkingSpaceIds { get; set; } = new();
}
