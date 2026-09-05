using System.Diagnostics.CodeAnalysis;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Corporate.Domain;

/// <summary>Append-only audit trail for Corporate SSO operations (SOC2-minded).</summary>
public class CorporateSsoAuditEvent : BaseEntity
{
    public Guid? CompanyId { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Outcome { get; private set; } = string.Empty;
    public string? ErrorCode { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public string? DetailJson { get; private set; }

    [ExcludeFromCodeCoverage]
    private CorporateSsoAuditEvent()
    {
    }

    public static CorporateSsoAuditEvent Create(
        string action,
        string outcome,
        Guid? companyId = null,
        Guid? userId = null,
        string? errorCode = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? detailJson = null)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ValidationException("action", "Action is required");
        if (string.IsNullOrWhiteSpace(outcome))
            throw new ValidationException("outcome", "Outcome is required");

        return new CorporateSsoAuditEvent
        {
            CompanyId = companyId,
            UserId = userId,
            Action = action.Trim(),
            Outcome = outcome.Trim(),
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim(),
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
            DetailJson = detailJson
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }
}
