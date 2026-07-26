using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Marketplace.Contracts.DTOs;

public sealed record LprAccessResultDto(
    bool AccessGranted,
    string Decision,
    string? DenialReasonCode,
    string? DenialMessage,
    Guid? BookingId,
    string? BookingReference,
    Guid ParkingSpaceId,
    string LicensePlateNormalized,
    string Direction,
    DateTime OccurredAtUtc,
    Guid? AttemptId,
    double? Confidence = null,
    string? ImageUrl = null
);

public sealed record ProcessLprEventRequest(
    string LicensePlate,
    Guid ParkingSpaceId,
    string Direction,
    DateTime? OccurredAtUtc = null,
    /// <summary>Optional recognition confidence 0–1 from the camera engine.</summary>
    double? Confidence = null,
    /// <summary>Optional public URL of a plate image already uploaded.</summary>
    string? ImageUrl = null,
    /// <summary>Optional base64 plate image (data URL or raw base64); stored and linked when provided.</summary>
    string? ImageBase64 = null
);

// ── Camera registry ──────────────────────────────────────────────────────────

public sealed record LprCameraKeyDto(
    Guid Id,
    Guid ParkingSpaceId,
    string Name,
    string KeyId,
    string SecretPrefix,
    bool IsEnabled,
    DateTime CreatedAt
);

/// <summary>Returned only once when a camera key is created (includes plaintext secret).</summary>
public sealed record LprCameraKeyCreatedDto(
    Guid Id,
    Guid ParkingSpaceId,
    string Name,
    string KeyId,
    string Secret,
    string SecretPrefix,
    bool IsEnabled,
    DateTime CreatedAt
);

public sealed record CreateLprCameraKeyRequest(
    string Name,
    string? KeyId = null
);

// ── Plate rules ──────────────────────────────────────────────────────────────

public sealed record LprPlateRuleDto(
    Guid Id,
    Guid ParkingSpaceId,
    string LicensePlateNormalized,
    LprPlateRuleType RuleType,
    string? Note,
    bool IsEnabled,
    DateTime CreatedAt
);

public sealed record CreateLprPlateRuleRequest(
    string LicensePlate,
    LprPlateRuleType RuleType,
    string? Note = null
);
