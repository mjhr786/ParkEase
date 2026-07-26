using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.ValueObjects;

namespace ParkingApp.Marketplace.Domain.Entities;

/// <summary>
/// Facility plate allow/deny rule for LPR enforcement.
/// </summary>
public class LprPlateRule : BaseEntity
{
    public Guid ParkingSpaceId { get; internal set; }
    public string LicensePlateNormalized { get; internal set; } = string.Empty;
    public LprPlateRuleType RuleType { get; internal set; }
    public string? Note { get; internal set; }
    public bool IsEnabled { get; internal set; } = true;
    public Guid CreatedByUserId { get; internal set; }

    internal LprPlateRule()
    {
    }

    public static LprPlateRule Create(
        Guid parkingSpaceId,
        string licensePlate,
        LprPlateRuleType ruleType,
        Guid createdByUserId,
        string? note = null)
    {
        if (parkingSpaceId == Guid.Empty)
            throw new ValidationException("parkingSpaceId", "Parking space is required");
        if (createdByUserId == Guid.Empty)
            throw new ValidationException("createdByUserId", "Creator is required");
        if (!Enum.IsDefined(ruleType))
            throw new ValidationException("ruleType", "Invalid rule type");

        var normalized = LicensePlate.Normalize(licensePlate);
        if (string.IsNullOrEmpty(normalized))
            throw new ValidationException("licensePlate", "License plate is required");

        return new LprPlateRule
        {
            ParkingSpaceId = parkingSpaceId,
            LicensePlateNormalized = normalized,
            RuleType = ruleType,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IsEnabled = true,
            CreatedByUserId = createdByUserId
        };
    }

    public void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled) return;
        IsEnabled = enabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNote(string? note)
    {
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
