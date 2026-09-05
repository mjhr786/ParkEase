using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Domain;
using Xunit;

namespace ParkingApp.UnitTests.Identity;

public class CompanySsoConfigurationTests
{
    [Fact]
    public void Enable_RequiresVerifiedDomain_AndSecret()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id",
            "protected-secret",
            null,
            null,
            null,
            passwordLoginAllowed: true,
            autoAcceptInvitationsOnSso: true,
            forceAuthn: false,
            allowJitProvisioning: false);

        var ex = Assert.Throws<BusinessRuleException>(() => cfg.Enable());
        Assert.Equal("sso_domain_required", ex.RuleName);

        var domain = cfg.AddDomain("acme.example");
        domain.MarkVerified();
        cfg.Enable();
        Assert.True(cfg.IsEnabled);
    }

    [Fact]
    public void Enable_BlockedWhenForceDisabled()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id",
            "protected-secret",
            null, null, null, true, true, false, false);
        var domain = cfg.AddDomain("acme.example");
        domain.MarkVerified();
        cfg.ForceDisableByPlatform(Guid.NewGuid());

        var ex = Assert.Throws<BusinessRuleException>(() => cfg.Enable());
        Assert.Equal("sso_force_disabled", ex.RuleName);
        Assert.False(cfg.IsEnabled);
    }

    [Fact]
    public void RemoveLastVerifiedDomain_DisablesSso()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id",
            "protected-secret",
            null, null, null, true, true, false, false);
        var domain = cfg.AddDomain("acme.example");
        domain.MarkVerified();
        cfg.Enable();
        Assert.True(cfg.IsEnabled);

        cfg.RemoveDomain(domain.Id);
        Assert.False(cfg.IsEnabled);
        Assert.True(domain.IsDeleted);
    }

    [Fact]
    public void AddDomain_RejectsDuplicate()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.AddDomain("Acme.Example");
        var ex = Assert.Throws<BusinessRuleException>(() => cfg.AddDomain("acme.example"));
        Assert.Equal("domain_exists", ex.RuleName);
    }

    [Fact]
    public void UpsertOidcSettings_RejectsHttpAuthority()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        Assert.Throws<ValidationException>(() =>
            cfg.UpsertOidcSettings(
                "http://insecure.example",
                "client",
                "secret",
                null, null, null, true, true, false, false));
    }

    [Fact]
    public void ClearForceDisable_DoesNotAutoEnable()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id",
            "protected-secret",
            null, null, null, true, true, false, false);
        var domain = cfg.AddDomain("acme.example");
        domain.MarkVerified();
        cfg.Enable();
        cfg.ForceDisableByPlatform(Guid.NewGuid());
        Assert.False(cfg.IsEnabled);
        Assert.True(cfg.ForceDisabledByPlatform);

        cfg.ClearForceDisableByPlatform();
        Assert.False(cfg.ForceDisabledByPlatform);
        Assert.False(cfg.IsEnabled);
    }

    [Fact]
    public void ForceDisable_DisablesAndRecordsActor_CompanyAdminCannotReEnableUntilClear()
    {
        var adminId = Guid.NewGuid();
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://login.microsoftonline.com/tenant/v2.0",
            "client-id",
            "protected-secret",
            null, null, null, true, true, false, false);
        var domain = cfg.AddDomain("acme.example");
        domain.MarkVerified();
        cfg.Enable();
        Assert.True(cfg.IsEnabled);

        cfg.ForceDisableByPlatform(adminId);
        Assert.False(cfg.IsEnabled);
        Assert.True(cfg.ForceDisabledByPlatform);
        Assert.Equal(adminId, cfg.ForceDisabledByUserId);
        Assert.NotNull(cfg.ForceDisabledAt);

        var blocked = Assert.Throws<BusinessRuleException>(() => cfg.Enable());
        Assert.Equal("sso_force_disabled", blocked.RuleName);

        cfg.ClearForceDisableByPlatform();
        Assert.False(cfg.ForceDisabledByPlatform);
        Assert.Null(cfg.ForceDisabledByUserId);
        Assert.Null(cfg.ForceDisabledAt);
        Assert.False(cfg.IsEnabled);

        // After clear, company admin can enable again.
        cfg.Enable();
        Assert.True(cfg.IsEnabled);
    }

    [Fact]
    public void ForceDisable_RequiresPlatformAdminUserId()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        Assert.Throws<ValidationException>(() => cfg.ForceDisableByPlatform(Guid.Empty));
    }
}
