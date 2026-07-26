namespace ParkingApp.Marketplace.Application.Options;

/// <summary>LPR access + overstay runtime options (Iot:Lpr section).</summary>
public sealed class LprAccessOptions
{
    public const string SectionName = "Iot:Lpr";

    /// <summary>
    /// When set to a value in (0, 1], camera-supplied confidence below this denies access.
    /// Null or 0 disables the gate (confidence still stored when provided).
    /// </summary>
    public double? MinConfidence { get; set; }

    public LprOverstayOptions Overstay { get; set; } = new();
}

public sealed class LprOverstayOptions
{
    /// <summary>When false, background overstay detection is disabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Minutes after booking EndDateTime before an overstay alert is sent.</summary>
    public int GraceMinutes { get; set; } = 15;

    /// <summary>Seconds between detection polls.</summary>
    public int PollIntervalSeconds { get; set; } = 120;

    public int BatchSize { get; set; } = 50;

    /// <summary>When true, assess monetary overstay fees after grace.</summary>
    public bool FeesEnabled { get; set; } = true;

    /// <summary>Multiplier on parking hourly rate for overstay (e.g. 1.5 = 150% of hourly).</summary>
    public decimal RateMultiplier { get; set; } = 1.5m;

    /// <summary>Minimum fee when any billable overstay exists.</summary>
    public decimal MinimumFee { get; set; } = 0m;

    /// <summary>Optional fee cap; null or 0 = no cap.</summary>
    public decimal? MaximumFee { get; set; }

    /// <summary>
    /// When true, force check-out of InProgress bookings that remain past
    /// EndDateTime + GraceMinutes + AutoCheckOutMinutes.
    /// </summary>
    public bool AutoCheckOutEnabled { get; set; } = true;

    /// <summary>
    /// Extra minutes after the grace window before automatic check-out.
    /// 0 = check out on the first poll after grace elapses.
    /// </summary>
    public int AutoCheckOutMinutes { get; set; } = 60;
}
