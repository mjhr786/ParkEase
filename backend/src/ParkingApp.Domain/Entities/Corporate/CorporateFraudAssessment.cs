using ParkingApp.Domain.Enums;

namespace ParkingApp.Domain.Entities.Corporate;

public sealed record CorporateFraudAssessment(
    CorporateFraudRiskLevel RiskLevel,
    bool IsBlocked,
    string? Reason)
{
    public static CorporateFraudAssessment None() => new(CorporateFraudRiskLevel.None, false, null);

    public static CorporateFraudAssessment Flag(CorporateFraudRiskLevel riskLevel, string reason) =>
        new(riskLevel, false, reason);

    public static CorporateFraudAssessment Block(CorporateFraudRiskLevel riskLevel, string reason) =>
        new(riskLevel, true, reason);
}
