using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Append-only audit of LPR gate decisions (grant or deny).
/// </summary>
public class LprAccessAttempt : BaseEntity
{
    public Guid ParkingSpaceId { get; internal set; }
    public string LicensePlateRaw { get; internal set; } = string.Empty;
    public string LicensePlateNormalized { get; internal set; } = string.Empty;
    public LprDirection Direction { get; internal set; }
    public DateTime OccurredAtUtc { get; internal set; }
    public LprAccessDecision Decision { get; internal set; }
    public string? DenialReason { get; internal set; }
    public Guid? BookingId { get; internal set; }
    public string Source { get; internal set; } = LprAccessSources.Iot;
    public string? ClientKeyId { get; internal set; }

    /// <summary>Optional LPR engine confidence 0–1.</summary>
    public double? Confidence { get; internal set; }

    /// <summary>Optional plate crop / capture image URL (public storage).</summary>
    public string? ImageUrl { get; internal set; }

    internal LprAccessAttempt()
    {
    }

    public static LprAccessAttempt CreateGranted(
        Guid parkingSpaceId,
        string licensePlateRaw,
        string licensePlateNormalized,
        LprDirection direction,
        DateTime occurredAtUtc,
        Guid bookingId,
        string source,
        string? clientKeyId = null,
        double? confidence = null,
        string? imageUrl = null)
    {
        ValidateCommon(parkingSpaceId, licensePlateRaw, licensePlateNormalized, occurredAtUtc, source, bookingId);

        return new LprAccessAttempt
        {
            ParkingSpaceId = parkingSpaceId,
            LicensePlateRaw = licensePlateRaw.Trim(),
            LicensePlateNormalized = licensePlateNormalized,
            Direction = direction,
            OccurredAtUtc = occurredAtUtc,
            Decision = LprAccessDecision.Granted,
            DenialReason = null,
            BookingId = bookingId,
            Source = source.Trim(),
            ClientKeyId = string.IsNullOrWhiteSpace(clientKeyId) ? null : clientKeyId.Trim(),
            Confidence = ClampConfidence(confidence),
            ImageUrl = NormalizeImageUrl(imageUrl)
        };
    }

    public static LprAccessAttempt CreateDenied(
        Guid parkingSpaceId,
        string licensePlateRaw,
        string? licensePlateNormalized,
        LprDirection direction,
        DateTime occurredAtUtc,
        string denialReasonCode,
        string source,
        string? clientKeyId = null,
        Guid? bookingId = null,
        double? confidence = null,
        string? imageUrl = null)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (string.IsNullOrWhiteSpace(denialReasonCode))
            throw new ValidationException("denialReasonCode", "Denial reason is required");
        if (string.IsNullOrWhiteSpace(source))
            throw new ValidationException("source", "Source is required");

        var raw = string.IsNullOrWhiteSpace(licensePlateRaw) ? string.Empty : licensePlateRaw.Trim();
        var normalized = LicensePlate.Normalize(licensePlateNormalized ?? licensePlateRaw) ?? string.Empty;

        return new LprAccessAttempt
        {
            ParkingSpaceId = parkingSpaceId,
            LicensePlateRaw = raw.Length > 50 ? raw[..50] : raw,
            LicensePlateNormalized = normalized,
            Direction = direction,
            OccurredAtUtc = occurredAtUtc,
            Decision = LprAccessDecision.Denied,
            DenialReason = denialReasonCode.Trim(),
            BookingId = bookingId,
            Source = source.Trim(),
            ClientKeyId = string.IsNullOrWhiteSpace(clientKeyId) ? null : clientKeyId.Trim(),
            Confidence = ClampConfidence(confidence),
            ImageUrl = NormalizeImageUrl(imageUrl)
        };
    }

    private static double? ClampConfidence(double? confidence)
    {
        if (!confidence.HasValue) return null;
        if (double.IsNaN(confidence.Value) || double.IsInfinity(confidence.Value))
            return null;
        return Math.Clamp(confidence.Value, 0d, 1d);
    }

    private static string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var url = imageUrl.Trim();
        return url.Length > 1000 ? url[..1000] : url;
    }

    private static void ValidateCommon(
        Guid parkingSpaceId,
        string licensePlateRaw,
        string licensePlateNormalized,
        DateTime occurredAtUtc,
        string source,
        Guid bookingId)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (bookingId == Guid.Empty)
            throw new ValidationException("bookingId", "Booking is required for granted access");
        if (string.IsNullOrWhiteSpace(licensePlateRaw))
            throw new ValidationException("licensePlateRaw", "Raw plate is required");
        if (string.IsNullOrWhiteSpace(licensePlateNormalized))
            throw new ValidationException("licensePlateNormalized", "Normalized plate is required");
        if (string.IsNullOrWhiteSpace(source))
            throw new ValidationException("source", "Source is required");
        if (occurredAtUtc == default)
            throw new ValidationException("occurredAtUtc", "Occurrence time is required");
    }
}
