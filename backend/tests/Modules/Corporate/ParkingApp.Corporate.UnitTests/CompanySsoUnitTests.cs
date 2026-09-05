using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Admin.Contracts;
using ParkingApp.Application.CQRS.Commands.Corporate.Sso;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Enums;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using Xunit;

namespace ParkingApp.Corporate.UnitTests;

public class CompanySsoCoverageTests
{
    private readonly Mock<ICorporateUnitOfWork> _uowMock = new();
    private readonly Mock<ICompanyRepository> _companyRepoMock = new();
    private readonly Mock<ICompanySsoAdminStore> _storeMock = new();
    private readonly Mock<ISsoSecretCipher> _cipherMock = new();
    private readonly Mock<ICorporateSsoPublicSettings> _settingsMock = new();
    private readonly Mock<IDnsTxtLookup> _dnsLookupMock = new();
    private readonly Mock<IOidcDiscoveryProbe> _probeMock = new();
    private readonly Mock<IAdminAudit> _adminAuditMock = new();

    public CompanySsoCoverageTests()
    {
        _uowMock.Setup(u => u.Companies).Returns(_companyRepoMock.Object);
        _settingsMock.Setup(s => s.OidcRedirectUri).Returns("https://app.parkease.test/auth/corporate/sso/callback");
        _settingsMock.Setup(s => s.GlobalEnabled).Returns(true);
        _settingsMock.Setup(s => s.IsCompanyAllowed(It.IsAny<Guid>())).Returns(true);
    }

    #region CompanySsoDomain Tests

    [Fact]
    public void CompanySsoDomain_NormalizeDomain_Valid_ReturnsLowercaseTrimmed()
    {
        var domain = CompanySsoDomain.NormalizeDomain("  WWW.Example.COM.  ");
        domain.Should().Be("example.com");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CompanySsoDomain_NormalizeDomain_Empty_ThrowsValidationException(string input)
    {
        var act = () => CompanySsoDomain.NormalizeDomain(input);
        act.Should().Throw<ValidationException>().WithMessage("*Domain is required*");
    }

    [Theory]
    [InlineData("invalid_domain")]
    [InlineData("-example.com")]
    [InlineData("example..com")]
    public void CompanySsoDomain_NormalizeDomain_InvalidFormat_ThrowsValidationException(string input)
    {
        var act = () => CompanySsoDomain.NormalizeDomain(input);
        act.Should().Throw<ValidationException>().WithMessage("*Invalid domain format*");
    }

    [Fact]
    public void CompanySsoDomain_Create_Valid_SetsFields()
    {
        var companyId = Guid.NewGuid();
        var configId = Guid.NewGuid();
        var domain = CompanySsoDomain.Create(companyId, configId, "corp.test");

        domain.CompanyId.Should().Be(companyId);
        domain.CompanySsoConfigurationId.Should().Be(configId);
        domain.Domain.Should().Be("corp.test");
        domain.IsVerified.Should().BeFalse();
        domain.VerificationToken.Should().NotBeNullOrWhiteSpace();
        domain.ExpectedTxtValue.Should().Be($"parkease-sso-verify={domain.VerificationToken}");
        domain.PreferredTxtHost.Should().Be("_parkease-sso.corp.test");
    }

    [Fact]
    public void CompanySsoDomain_Create_EmptyCompanyId_ThrowsValidationException()
    {
        var act = () => CompanySsoDomain.Create(Guid.Empty, Guid.NewGuid(), "corp.test");
        act.Should().Throw<ValidationException>().WithMessage("*Company id is required*");
    }

    [Fact]
    public void CompanySsoDomain_Create_EmptyConfigId_ThrowsValidationException()
    {
        var act = () => CompanySsoDomain.Create(Guid.NewGuid(), Guid.Empty, "corp.test");
        act.Should().Throw<ValidationException>().WithMessage("*Configuration id is required*");
    }

    [Fact]
    public void CompanySsoDomain_MarkVerified_SetsIsVerifiedAndDate()
    {
        var domain = CompanySsoDomain.Create(Guid.NewGuid(), Guid.NewGuid(), "corp.test");
        var now = DateTime.UtcNow;

        domain.MarkVerified(now);

        domain.IsVerified.Should().BeTrue();
        domain.VerifiedAt.Should().Be(now);
    }

    [Fact]
    public void CompanySsoDomain_Unverify_ResetsVerificationState()
    {
        var domain = CompanySsoDomain.Create(Guid.NewGuid(), Guid.NewGuid(), "corp.test");
        domain.MarkVerified();
        var oldToken = domain.VerificationToken;

        domain.Unverify();

        domain.IsVerified.Should().BeFalse();
        domain.VerifiedAt.Should().BeNull();
        domain.VerificationToken.Should().NotBe(oldToken);
    }

    [Fact]
    public void CompanySsoDomain_RotateVerificationToken_ChangesToken()
    {
        var domain = CompanySsoDomain.Create(Guid.NewGuid(), Guid.NewGuid(), "corp.test");
        var oldToken = domain.VerificationToken;

        domain.RotateVerificationToken();

        domain.VerificationToken.Should().NotBe(oldToken);
    }

    #endregion

    #region CompanySsoConfiguration Tests

    [Fact]
    public void CompanySsoConfiguration_Create_EmptyCompanyId_ThrowsValidationException()
    {
        var act = () => CompanySsoConfiguration.Create(Guid.Empty);
        act.Should().Throw<ValidationException>().WithMessage("*Company id is required*");
    }

    [Fact]
    public void CompanySsoConfiguration_Create_Valid_InitializesDefaults()
    {
        var companyId = Guid.NewGuid();
        var cfg = CompanySsoConfiguration.Create(companyId);

        cfg.CompanyId.Should().Be(companyId);
        cfg.Protocol.Should().Be(SsoProtocol.Oidc);
        cfg.IsEnabled.Should().BeFalse();
        cfg.PasswordLoginAllowed.Should().BeTrue();
        cfg.AutoAcceptInvitationsOnSso.Should().BeTrue();
        cfg.HasClientSecret.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://insecure.test")]
    [InlineData("ftp://invalid.test")]
    public void CompanySsoConfiguration_UpsertOidcSettings_InvalidAuthority_ThrowsValidationException(string auth)
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.UpsertOidcSettings(auth, "client123", "secret", null, null, null, true, true, false, false);
        act.Should().Throw<ValidationException>().WithMessage("*Authority must be an https URL*");
    }

    [Fact]
    public void CompanySsoConfiguration_UpsertOidcSettings_EmptyClientId_ThrowsValidationException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.UpsertOidcSettings("https://auth.example.com", "", "secret", null, null, null, true, true, false, false);
        act.Should().Throw<ValidationException>().WithMessage("*Client id is required*");
    }

    [Fact]
    public void CompanySsoConfiguration_UpsertOidcSettings_InvalidAuthMethod_ThrowsValidationException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.UpsertOidcSettings("https://auth.example.com", "c123", "secret", null, null, "invalid_method", true, true, false, false);
        act.Should().Throw<ValidationException>().WithMessage("*TokenEndpointAuthMethod must be client_secret_post or client_secret_basic*");
    }

    [Fact]
    public void CompanySsoConfiguration_UpsertOidcSettings_Valid_UpdatesFields()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings(
            "https://auth.example.com/",
            " client123 ",
            "enc_secret",
            " https://auth.example.com/.well-known/openid-configuration ",
            "{\"email\":\"email\"}",
            "client_secret_post",
            false,
            true,
            true,
            true);

        cfg.Authority.Should().Be("https://auth.example.com");
        cfg.ClientId.Should().Be("client123");
        cfg.ClientSecretProtected.Should().Be("enc_secret");
        cfg.MetadataUrl.Should().Be("https://auth.example.com/.well-known/openid-configuration");
        cfg.AttributeMappingJson.Should().Be("{\"email\":\"email\"}");
        cfg.TokenEndpointAuthMethod.Should().Be("client_secret_post");
        cfg.PasswordLoginAllowed.Should().BeFalse();
        cfg.AutoAcceptInvitationsOnSso.Should().BeTrue();
        cfg.ForceAuthn.Should().BeTrue();
        cfg.AllowJitProvisioning.Should().BeTrue();
        cfg.HasClientSecret.Should().BeTrue();
    }

    [Fact]
    public void CompanySsoConfiguration_SetClientSecretProtected_UpdatesSecret()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.SetClientSecretProtected("cipher_123");
        cfg.ClientSecretProtected.Should().Be("cipher_123");

        cfg.SetClientSecretProtected("   ");
        cfg.ClientSecretProtected.Should().BeNull();
    }

    [Fact]
    public void CompanySsoConfiguration_RecordTestResult_TruncatesLongCode()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var longCode = new string('A', 100);
        cfg.RecordTestResult(longCode);

        cfg.LastTestedAt.Should().NotBeNull();
        cfg.LastTestResult.Should().HaveLength(64);
    }

    [Fact]
    public void CompanySsoConfiguration_Enable_WithoutAuthorityOrClientId_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.Enable();
        act.Should().Throw<BusinessRuleException>().WithMessage("*Authority and ClientId are required*");
    }

    [Fact]
    public void CompanySsoConfiguration_Enable_WithoutSecret_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings("https://auth.test", "client", null, null, null, null, true, true, false, false);
        var act = () => cfg.Enable();
        act.Should().Throw<BusinessRuleException>().WithMessage("*Client secret is required*");
    }

    [Fact]
    public void CompanySsoConfiguration_Enable_WithoutVerifiedDomain_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings("https://auth.test", "client", "secret", null, null, null, true, true, false, false);
        var act = () => cfg.Enable();
        act.Should().Throw<BusinessRuleException>().WithMessage("*At least one verified domain is required*");
    }

    [Fact]
    public void CompanySsoConfiguration_Enable_WhenForceDisabledByPlatform_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.ForceDisableByPlatform(Guid.NewGuid());
        var act = () => cfg.Enable();
        act.Should().Throw<BusinessRuleException>().WithMessage("*SSO is force-disabled by platform*");
    }

    [Fact]
    public void CompanySsoConfiguration_Enable_Valid_EnablesSso()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings("https://auth.test", "client", "secret", null, null, null, true, true, false, false);
        var dom = cfg.AddDomain("corp.test");
        dom.MarkVerified();

        cfg.Enable();

        cfg.IsEnabled.Should().BeTrue();
        cfg.EnabledAt.Should().NotBeNull();
        cfg.DisabledAt.Should().BeNull();
    }

    [Fact]
    public void CompanySsoConfiguration_Disable_DisablesSso()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings("https://auth.test", "client", "secret", null, null, null, true, true, false, false);
        var dom = cfg.AddDomain("corp.test");
        dom.MarkVerified();
        cfg.Enable();

        cfg.Disable();

        cfg.IsEnabled.Should().BeFalse();
        cfg.DisabledAt.Should().NotBeNull();
    }

    [Fact]
    public void CompanySsoConfiguration_ForceDisableByPlatform_SetsLock()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var adminId = Guid.NewGuid();

        cfg.ForceDisableByPlatform(adminId);

        cfg.ForceDisabledByPlatform.Should().BeTrue();
        cfg.ForceDisabledByUserId.Should().Be(adminId);
        cfg.ForceDisabledAt.Should().NotBeNull();
        cfg.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CompanySsoConfiguration_ForceDisableByPlatform_EmptyAdminId_ThrowsValidationException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.ForceDisableByPlatform(Guid.Empty);
        act.Should().Throw<ValidationException>().WithMessage("*Platform admin user id is required*");
    }

    [Fact]
    public void CompanySsoConfiguration_ClearForceDisableByPlatform_ClearsLock()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.ForceDisableByPlatform(Guid.NewGuid());

        cfg.ClearForceDisableByPlatform();

        cfg.ForceDisabledByPlatform.Should().BeFalse();
        cfg.ForceDisabledByUserId.Should().BeNull();
        cfg.ForceDisabledAt.Should().BeNull();
    }

    [Fact]
    public void CompanySsoConfiguration_AddDomain_DuplicateDomain_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.AddDomain("corp.test");

        var act = () => cfg.AddDomain("corp.test");
        act.Should().Throw<BusinessRuleException>().WithMessage("*Domain corp.test is already configured*");
    }

    [Fact]
    public void CompanySsoConfiguration_RemoveDomain_NotFound_ThrowsBusinessRuleException()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var act = () => cfg.RemoveDomain(Guid.NewGuid());
        act.Should().Throw<BusinessRuleException>().WithMessage("*SSO domain was not found*");
    }

    [Fact]
    public void CompanySsoConfiguration_RemoveDomain_LastVerifiedDomain_DisablesSso()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        cfg.UpsertOidcSettings("https://auth.test", "client", "secret", null, null, null, true, true, false, false);
        var dom = cfg.AddDomain("corp.test");
        dom.MarkVerified();
        cfg.Enable();

        cfg.RemoveDomain(dom.Id);

        dom.IsDeleted.Should().BeTrue();
        cfg.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CompanySsoConfiguration_RequireDomain_ReturnsDomainOrThrows()
    {
        var cfg = CompanySsoConfiguration.Create(Guid.NewGuid());
        var dom = cfg.AddDomain("corp.test");

        cfg.RequireDomain(dom.Id).Should().Be(dom);

        var act = () => cfg.RequireDomain(Guid.NewGuid());
        act.Should().Throw<BusinessRuleException>().WithMessage("*SSO domain was not found*");
    }

    #endregion

    #region CorporateSsoAuditEvent Tests

    [Fact]
    public void CorporateSsoAuditEvent_Create_EmptyAction_ThrowsValidationException()
    {
        var act = () => CorporateSsoAuditEvent.Create("", "SUCCESS");
        act.Should().Throw<ValidationException>().WithMessage("*Action is required*");
    }

    [Fact]
    public void CorporateSsoAuditEvent_Create_EmptyOutcome_ThrowsValidationException()
    {
        var act = () => CorporateSsoAuditEvent.Create("LOGIN", "");
        act.Should().Throw<ValidationException>().WithMessage("*Outcome is required*");
    }

    [Fact]
    public void CorporateSsoAuditEvent_Create_Valid_TruncatesLongIpAndUserAgent()
    {
        var longIp = new string('1', 100);
        var longUa = new string('A', 600);

        var evt = CorporateSsoAuditEvent.Create(
            "LOGIN",
            "SUCCESS",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invalid_code",
            longIp,
            longUa,
            "{\"detail\":true}");

        evt.Action.Should().Be("LOGIN");
        evt.Outcome.Should().Be("SUCCESS");
        evt.ErrorCode.Should().Be("invalid_code");
        evt.IpAddress.Should().HaveLength(64);
        evt.UserAgent.Should().HaveLength(512);
        evt.DetailJson.Should().Be("{\"detail\":true}");
    }

    #endregion

    #region CompanySsoAdminShared Tests

    [Fact]
    public void CompanySsoAdminShared_ParseProtocol_ValidOidc_ReturnsOidc()
    {
        CompanySsoAdminShared.ParseProtocol("OIDC").Should().Be(SsoProtocol.Oidc);
        CompanySsoAdminShared.ParseProtocol(null).Should().Be(SsoProtocol.Oidc);
    }

    [Fact]
    public void CompanySsoAdminShared_ParseProtocol_Unsupported_ThrowsValidationException()
    {
        var act = () => CompanySsoAdminShared.ParseProtocol("SAML2");
        act.Should().Throw<ValidationException>().WithMessage("*Only Oidc is supported in MVP*");
    }

    [Fact]
    public void CompanySsoAdminShared_FromDomainException_MapsCodeCorrectly()
    {
        var bre = new BusinessRuleException("custom_rule", "rule text");
        var res1 = CompanySsoAdminShared.FromDomainException<object>(bre);
        res1.Code.Should().Be("custom_rule");

        var ve = new ValidationException("field", "validation text");
        var res2 = CompanySsoAdminShared.FromDomainException<object>(ve);
        res2.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task CompanySsoAdminShared_EnsureCompanyAdminAsync_CompanyNotFound_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var res = await CompanySsoAdminShared.EnsureCompanyAdminAsync<object>(
            _uowMock.Object, companyId, adminId, CancellationToken.None);

        res.Should().NotBeNull();
        res!.Code.Should().Be("company_not_found");
    }

    [Fact]
    public async Task CompanySsoAdminShared_EnsureCompanyAdminAsync_NonAdmin_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("TestCorp", "REG123", "admin@testcorp.com", "+1234567890", "123 Main St", BillingType.ReservedSlots, adminId);
        var membership = UserCompanyMembership.Create(adminId, companyId, CompanyRole.Employee);

        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyRepoMock.Setup(c => c.GetMembershipAsync(companyId, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var res = await CompanySsoAdminShared.EnsureCompanyAdminAsync<object>(
            _uowMock.Object, companyId, adminId, CancellationToken.None);

        res.Should().NotBeNull();
        res!.Code.Should().Be("company_admin_required");
    }

    #endregion

    #region Handlers Tests

    private void SetupActiveAdmin(Guid companyId, Guid adminId)
    {
        var company = Company.Create("TestCorp", "REG123", "admin@testcorp.com", "+1234567890", "123 Main St", BillingType.ReservedSlots, adminId);
        var membership = UserCompanyMembership.Create(adminId, companyId, CompanyRole.Admin);
        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _companyRepoMock.Setup(c => c.GetMembershipAsync(companyId, adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
    }

    [Fact]
    public async Task GetCompanySsoConfigHandler_NoConfig_ReturnsEmptyShellDto()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new GetCompanySsoConfigHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new GetCompanySsoConfigQuery(companyId, adminId));

        res.Success.Should().BeTrue();
        res.Data!.CompanyId.Should().Be(companyId);
        res.Data.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpsertCompanySsoHandler_Valid_CreatesOrUpdatesConfig()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);
        _cipherMock.Setup(c => c.Protect("secret123")).Returns("cipher_secret123");

        var handler = new UpsertCompanySsoHandler(
            _uowMock.Object,
            _storeMock.Object,
            _cipherMock.Object,
            _settingsMock.Object,
            NullLogger<UpsertCompanySsoHandler>.Instance);

        var dto = new UpsertCompanySsoDto(
            "Oidc",
            "https://auth.example.com",
            "client123",
            "secret123",
            null,
            null,
            "client_secret_post",
            true,
            true,
            false,
            false);

        var cmd = new UpsertCompanySsoCommand(companyId, adminId, dto);

        var res = await handler.HandleAsync(cmd);

        res.Success.Should().BeTrue();
        _storeMock.Verify(s => s.AddAsync(It.IsAny<CompanySsoConfiguration>(), It.IsAny<CancellationToken>()), Times.Once);
        _storeMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddCompanySsoDomainHandler_Valid_AddsDomain()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        _storeMock.Setup(s => s.IsDomainVerifiedByOtherCompanyAsync("corp.test", companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new AddCompanySsoDomainHandler(
            _uowMock.Object,
            _storeMock.Object);

        var res = await handler.HandleAsync(new AddCompanySsoDomainCommand(companyId, adminId, "corp.test"));

        res.Success.Should().BeTrue();
        cfg.Domains.Should().ContainSingle(d => d.Domain == "corp.test");
    }

    [Fact]
    public async Task VerifyCompanySsoDomainHandler_TxtMatches_VerifiesDomain()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        var dom = cfg.AddDomain("corp.test");

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        _dnsLookupMock.Setup(d => d.LookupTxtAsync(dom.PreferredTxtHost, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { dom.ExpectedTxtValue });

        var handler = new VerifyCompanySsoDomainHandler(
            _uowMock.Object,
            _storeMock.Object,
            _dnsLookupMock.Object);

        var res = await handler.HandleAsync(new VerifyCompanySsoDomainCommand(companyId, adminId, dom.Id));

        res.Success.Should().BeTrue();
        dom.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyCompanySsoDomainHandler_ConfigNull_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new VerifyCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object, _dnsLookupMock.Object);
        var res = await handler.HandleAsync(new VerifyCompanySsoDomainCommand(companyId, adminId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("SSO is not configured");
    }

    [Fact]
    public async Task VerifyCompanySsoDomainHandler_DomainVerifiedByOtherCompany_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        var dom = cfg.AddDomain("corp.test");

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        _storeMock.Setup(s => s.IsDomainVerifiedByOtherCompanyAsync("corp.test", companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new VerifyCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object, _dnsLookupMock.Object);
        var res = await handler.HandleAsync(new VerifyCompanySsoDomainCommand(companyId, adminId, dom.Id));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("already verified");
    }

    [Fact]
    public async Task VerifyCompanySsoDomainHandler_TxtUnmatched_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        var dom = cfg.AddDomain("corp.test");

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        _storeMock.Setup(s => s.IsDomainVerifiedByOtherCompanyAsync("corp.test", companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _dnsLookupMock.Setup(d => d.LookupTxtAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "wrong_txt_value" });

        var handler = new VerifyCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object, _dnsLookupMock.Object);
        var res = await handler.HandleAsync(new VerifyCompanySsoDomainCommand(companyId, adminId, dom.Id));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task VerifyCompanySsoDomainHandler_ApexFallbackMatch_VerifiesDomain()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        var dom = cfg.AddDomain("corp.test");

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        _storeMock.Setup(s => s.IsDomainVerifiedByOtherCompanyAsync("corp.test", companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _dnsLookupMock.Setup(d => d.LookupTxtAsync(dom.PreferredTxtHost, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        _dnsLookupMock.Setup(d => d.LookupTxtAsync("corp.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { $"some-prefix {dom.ExpectedTxtValue} some-suffix" });

        var handler = new VerifyCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object, _dnsLookupMock.Object);
        var res = await handler.HandleAsync(new VerifyCompanySsoDomainCommand(companyId, adminId, dom.Id));

        res.Success.Should().BeTrue();
        dom.IsVerified.Should().BeTrue();
    }


    [Fact]
    public async Task ForceDisableCompanySsoHandler_Valid_SetsForceDisabled()
    {
        var companyId = Guid.NewGuid();
        var platformAdminId = Guid.NewGuid();
        var cfg = CompanySsoConfiguration.Create(companyId);
        var company = Company.Create("TestCorp", "REG123", "admin@testcorp.com", "+1234567890", "123 Main St", BillingType.ReservedSlots, platformAdminId);

        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new ForceDisableCompanySsoHandler(
            _uowMock.Object,
            _storeMock.Object,
            _cipherMock.Object,
            _settingsMock.Object,
            _adminAuditMock.Object,
            NullLogger<ForceDisableCompanySsoHandler>.Instance);

        var res = await handler.HandleAsync(new ForceDisableCompanySsoCommand(companyId, platformAdminId, "admin@platform.test", "Security review", "127.0.0.1", "TestUA"));

        res.Success.Should().BeTrue();
        cfg.ForceDisabledByPlatform.Should().BeTrue();
    }

    [Fact]
    public async Task ClearForceDisableCompanySsoHandler_Valid_ClearsLock()
    {
        var companyId = Guid.NewGuid();
        var platformAdminId = Guid.NewGuid();
        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.ForceDisableByPlatform(platformAdminId);
        var company = Company.Create("TestCorp", "REG123", "admin@testcorp.com", "+1234567890", "123 Main St", BillingType.ReservedSlots, platformAdminId);

        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new ClearForceDisableCompanySsoHandler(
            _uowMock.Object,
            _storeMock.Object,
            _cipherMock.Object,
            _settingsMock.Object,
            _adminAuditMock.Object,
            NullLogger<ClearForceDisableCompanySsoHandler>.Instance);

        var res = await handler.HandleAsync(new ClearForceDisableCompanySsoCommand(companyId, platformAdminId, "admin@platform.test", "Cleared", "127.0.0.1", "TestUA"));

        res.Success.Should().BeTrue();
        cfg.ForceDisabledByPlatform.Should().BeFalse();
    }

    [Fact]
    public async Task EnableCompanySsoHandler_NoConfig_ReturnsSsoNotConfigured()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new EnableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new EnableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_not_configured");
    }

    [Fact]
    public async Task EnableCompanySsoHandler_ForceDisabled_ReturnsSsoForceDisabled()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.ForceDisableByPlatform(Guid.NewGuid());

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new EnableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new EnableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_force_disabled");
    }

    [Fact]
    public async Task EnableCompanySsoHandler_UnreadableSecret_ReturnsSecretUnreadable()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "corrupted_secret", null, null, null, true, true, false, false);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        string? nullStr = null;
        _cipherMock.Setup(c => c.TryUnprotect("corrupted_secret", out nullStr)).Returns(false);

        var handler = new EnableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new EnableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be(ISsoSecretCipher.SecretUnreadableCode);
    }

    [Fact]
    public async Task EnableCompanySsoHandler_NoVerifiedDomain_ReturnsDomainUnverified()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "valid_secret", null, null, null, true, true, false, false);
        string outVal = "plain_secret";
        _cipherMock.Setup(c => c.TryUnprotect("valid_secret", out outVal)).Returns(true);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new EnableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new EnableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("domain_unverified");
    }

    [Fact]
    public async Task EnableCompanySsoHandler_Valid_EnablesAndAudits()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "valid_secret", null, null, null, true, true, false, false);
        var dom = cfg.AddDomain("corp.test");
        dom.MarkVerified();
        string outVal = "plain_secret";
        _cipherMock.Setup(c => c.TryUnprotect("valid_secret", out outVal)).Returns(true);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new EnableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new EnableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeTrue();
        cfg.IsEnabled.Should().BeTrue();
        _storeMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableCompanySsoHandler_Valid_DisablesSso()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "valid_secret", null, null, null, true, true, false, false);
        var dom = cfg.AddDomain("corp.test");
        dom.MarkVerified();
        cfg.Enable();

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new DisableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new DisableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeTrue();
        cfg.IsEnabled.Should().BeFalse();
        _storeMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisableCompanySsoHandler_NoConfig_ReturnsSsoNotConfigured()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new DisableCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new DisableCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_not_configured");
    }

    [Fact]
    public async Task TestCompanySsoHandler_NoConfig_ReturnsSsoNotConfigured()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new TestCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _probeMock.Object);
        var res = await handler.HandleAsync(new TestCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_not_configured");
    }

    [Fact]
    public async Task TestCompanySsoHandler_IncompleteConfig_ReturnsIncomplete()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new TestCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _probeMock.Object);
        var res = await handler.HandleAsync(new TestCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_config_incomplete");
    }

    [Fact]
    public async Task TestCompanySsoHandler_MissingSecret_ReturnsSecretRequired()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", null, null, null, null, true, true, false, false);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new TestCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _probeMock.Object);
        var res = await handler.HandleAsync(new TestCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_secret_required");
    }

    [Fact]
    public async Task TestCompanySsoHandler_UnreadableSecret_ReturnsSecretUnreadable()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "bad_secret", null, null, null, true, true, false, false);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        string? nullStr = null;
        _cipherMock.Setup(c => c.TryUnprotect("bad_secret", out nullStr)).Returns(false);

        var handler = new TestCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _probeMock.Object);
        var res = await handler.HandleAsync(new TestCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeFalse();
        res.Code.Should().Be(ISsoSecretCipher.SecretUnreadableCode);
    }

    [Fact]
    public async Task TestCompanySsoHandler_ProbeSuccess_ReturnsSuccess()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        cfg.UpsertOidcSettings("https://auth.test", "client123", "good_secret", null, null, null, true, true, false, false);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);
        string outVal = "plain";
        _cipherMock.Setup(c => c.TryUnprotect("good_secret", out outVal)).Returns(true);
        _probeMock.Setup(p => p.ProbeAsync("https://auth.test", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OidcDiscoveryProbeResult(true, "ok", "https://auth.test", "Success"));

        var handler = new TestCompanySsoHandler(_uowMock.Object, _storeMock.Object, _cipherMock.Object, _probeMock.Object);
        var res = await handler.HandleAsync(new TestCompanySsoCommand(companyId, adminId));

        res.Success.Should().BeTrue();
        res.Data!.ResultCode.Should().Be("ok");
        cfg.LastTestResult.Should().Be("ok");
    }

    [Fact]
    public async Task RemoveCompanySsoDomainHandler_NoConfig_ReturnsSsoNotConfigured()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanySsoConfiguration?)null);

        var handler = new RemoveCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new RemoveCompanySsoDomainCommand(companyId, adminId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("sso_not_configured");
    }

    [Fact]
    public async Task RemoveCompanySsoDomainHandler_DomainNotFound_ReturnsDomainNotFound()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new RemoveCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new RemoveCompanySsoDomainCommand(companyId, adminId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("domain_not_found");
    }

    [Fact]
    public async Task RemoveCompanySsoDomainHandler_Valid_RemovesDomain()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var cfg = CompanySsoConfiguration.Create(companyId);
        var dom = cfg.AddDomain("corp.test");

        _storeMock.Setup(s => s.GetTrackedWithDomainsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cfg);

        var handler = new RemoveCompanySsoDomainHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new RemoveCompanySsoDomainCommand(companyId, adminId, dom.Id));

        res.Success.Should().BeTrue();
        dom.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task ListPlatformCorporateSsoHandler_Valid_ReturnsPaginatedList()
    {
        var companyId = Guid.NewGuid();
        var summary = new PlatformCompanySsoListRow(
            companyId,
            "TestCorp",
            "testcorp",
            true,
            true,
            false,
            null,
            null,
            "OIDC",
            "https://auth.test",
            DateTime.UtcNow,
            "ok",
            1,
            DateTime.UtcNow);

        _storeMock.Setup(s => s.ListPlatformSsoConfigsAsync(null, false, false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<PlatformCompanySsoListRow> { summary }, 1));
        _storeMock.Setup(s => s.CountSuccessfulSsoLoginsByCompanyAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, int> { { companyId, 42 } });

        var handler = new ListPlatformCorporateSsoHandler(_storeMock.Object, _settingsMock.Object);
        var res = await handler.HandleAsync(new ListPlatformCorporateSsoQuery(null, false, false, 1, 10));

        res.Success.Should().BeTrue();
        res.Data!.TotalCount.Should().Be(1);
        res.Data.Items.Should().ContainSingle();
        res.Data.Items[0].SsoLoginSuccessCount7d.Should().Be(42);
    }

    [Fact]
    public async Task GetPlatformCompanySsoAuditHandler_CompanyNotFound_ReturnsError()
    {
        var companyId = Guid.NewGuid();
        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new GetPlatformCompanySsoAuditHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new GetPlatformCompanySsoAuditQuery(companyId, 50));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("company_not_found");
    }

    [Fact]
    public async Task GetPlatformCompanySsoAuditHandler_Valid_ReturnsEvents()
    {
        var companyId = Guid.NewGuid();
        var company = Company.Create("TestCorp", "REG123", "admin@testcorp.com", "+1234567890", "123 Main St", BillingType.ReservedSlots, Guid.NewGuid());
        _companyRepoMock.Setup(c => c.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var evt = CorporateSsoAuditEvent.Create("LOGIN", "SUCCESS", companyId, Guid.NewGuid(), null, "127.0.0.1", "UA");
        _storeMock.Setup(s => s.GetRecentAuditAsync(companyId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([evt]);

        var handler = new GetPlatformCompanySsoAuditHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new GetPlatformCompanySsoAuditQuery(companyId, 50));

        res.Success.Should().BeTrue();
        res.Data.Should().ContainSingle(e => e.Action == "LOGIN");
    }

    [Fact]
    public async Task GetCompanySsoAuditHandler_Valid_ReturnsEvents()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var evt = CorporateSsoAuditEvent.Create("LOGIN", "SUCCESS", companyId, adminId, null, "127.0.0.1", "UA");
        _storeMock.Setup(s => s.GetRecentAuditAsync(companyId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([evt]);

        var handler = new GetCompanySsoAuditHandler(_uowMock.Object, _storeMock.Object);
        var res = await handler.HandleAsync(new GetCompanySsoAuditQuery(companyId, adminId, 50));

        res.Success.Should().BeTrue();
        res.Data.Should().ContainSingle(e => e.Action == "LOGIN");
    }

    [Fact]
    public void CorporateDto_Properties_CanBeInstantiated()
    {
        var summary = new PlatformCompanySsoSummaryDto(
            Guid.NewGuid(), "Corp", "slug", true, true, false, true, null, null, "OIDC", "https://auth", DateTime.UtcNow, "ok", 1, 10, DateTime.UtcNow);
        summary.CompanyName.Should().Be("Corp");

        var page = new PlatformCompanySsoPageDto(new List<PlatformCompanySsoSummaryDto> { summary }, 1, 10, 1);
        page.TotalCount.Should().Be(1);

        var testRes = new CompanySsoTestResultDto(true, "ok", "https://auth", "msg");
        testRes.Success.Should().BeTrue();

        var auditEvt = new CorporateSsoAuditEventDto(Guid.NewGuid(), "LOGIN", "SUCCESS", null, Guid.NewGuid(), DateTime.UtcNow, "{}");
        auditEvt.Action.Should().Be("LOGIN");

        var expiring = new ExpiringAllocationDto(Guid.NewGuid(), "Zone A", DateTime.UtcNow, "REF123", ParkingAllocationSource.CompanyOwned, 100.00m);
        expiring.ParkingSpaceTitle.Should().Be("Zone A");

        var vendorAlloc = new VendorParkingAllocationDto(
            Guid.NewGuid(), Guid.NewGuid(), "Company A", Guid.NewGuid(), "Lot 1", 10, 5, 5, 500m, DateTime.UtcNow, DateTime.UtcNow.AddMonths(1), AllocationStatus.Active, ParkingAllocationSource.VendorLease, Guid.NewGuid(), "REF", Guid.NewGuid(), DateTime.UtcNow, null, DateTime.UtcNow);
        vendorAlloc.TotalSlots.Should().Be(10);

        var fraud = new FraudAlertDto(Guid.NewGuid(), "User A", 1, 2, 85);
        fraud.RiskScore.Should().Be(85);

        var util = new AllocationUtilizationDto(Guid.NewGuid(), "Zone A", 20, 15, 75.0);
        util.UtilizationPercent.Should().Be(75.0);

        var peak = new PeakHourDto(14, 45);
        peak.HourOfDay.Should().Be(14);
    }

    [Fact]
    public async Task UnlinkCompanySsoIdentityHandler_LinkNotFound_ReturnsFail()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var linkAdminMock = new Mock<ICorporateSsoLinkAdmin>();
        linkAdminMock.Setup(l => l.DisableLinkAsync(companyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new UnlinkCompanySsoIdentityHandler(_uowMock.Object, _storeMock.Object, linkAdminMock.Object);
        var res = await handler.HandleAsync(new UnlinkCompanySsoIdentityCommand(companyId, adminId, Guid.NewGuid()));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("link_not_found");
    }

    [Fact]
    public async Task UnlinkCompanySsoIdentityHandler_Valid_UnlinksAndAudits()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        SetupActiveAdmin(companyId, adminId);

        var linkAdminMock = new Mock<ICorporateSsoLinkAdmin>();
        linkAdminMock.Setup(l => l.DisableLinkAsync(companyId, linkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new UnlinkCompanySsoIdentityHandler(_uowMock.Object, _storeMock.Object, linkAdminMock.Object);
        var res = await handler.HandleAsync(new UnlinkCompanySsoIdentityCommand(companyId, adminId, linkId));

        res.Success.Should().BeTrue();
        _storeMock.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
