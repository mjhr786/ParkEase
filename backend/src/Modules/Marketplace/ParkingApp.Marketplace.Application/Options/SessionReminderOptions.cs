namespace ParkingApp.Marketplace.Application.Options;

/// <summary>Pre-end session reminder settings (Marketplace:SessionReminders).</summary>
public sealed class SessionReminderOptions
{
    public const string SectionName = "Marketplace:SessionReminders";

    /// <summary>When false, background session reminders are disabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Notify when EndDateTime is within this many minutes from now (and still in the future).
    /// </summary>
    public int LeadMinutes { get; set; } = 30;

    /// <summary>Seconds between detection polls.</summary>
    public int PollIntervalSeconds { get; set; } = 60;

    public int BatchSize { get; set; } = 50;
}
