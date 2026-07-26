namespace ParkingApp.Marketplace.Application.Options;

/// <summary>Config for valet vehicle retrieval lead times.</summary>
public sealed class ValetOptions
{
    public const string SectionName = "Marketplace:Valet";

    /// <summary>Default minutes until target ready when guest does not specify.</summary>
    public int DefaultLeadMinutes { get; set; } = 10;

    public int MinLeadMinutes { get; set; } = 5;

    public int MaxLeadMinutes { get; set; } = 60;
}
