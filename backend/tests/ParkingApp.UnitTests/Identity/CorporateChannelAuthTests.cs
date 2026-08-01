using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Corporate.Contracts;
using ParkingApp.Identity.Application.Commands.Auth;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Infrastructure.Persistence;
using Xunit;

namespace ParkingApp.UnitTests.Identity;

public class CorporateChannelAuthTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ICompanyMembershipLookup> _memberships = new();
    private readonly Mock<ILogger<CorporateLoginHandler>> _loginLog = new();
    private readonly Mock<ILogger<SwitchChannelHandler>> _switchLog = new();

    public CorporateChannelAuthTests()
    {
        _uow.Setup(u => u.Users).Returns(_users.Object);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string p, string hash) => hash == $"hash:{p}");
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns((string p) => $"hash:{p}");
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()))
            .Returns("access");
        _tokens.Setup(t => t.GenerateRefreshToken()).Returns("refresh");
    }

    private static User ActiveUser(string email = "u@test.com") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        PasswordHash = "hash:secret",
        FirstName = "A",
        LastName = "B",
        PhoneNumber = "1",
        IsActive = true,
        Role = UserRole.User
    };

    [Fact]
    public async Task CorporateLogin_ZeroMemberships_ReturnsBootstrapSession()
    {
        var user = ActiveUser();
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _memberships.Setup(m => m.GetActiveMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CompanyMembershipSummary>());

        var handler = new CorporateLoginHandler(_uow.Object, _tokens.Object, _hasher.Object, _memberships.Object, _loginLog.Object);
        var res = await handler.HandleAsync(new CorporateLoginCommand(new CorporateLoginDto(user.Email!, "secret")));

        res.Success.Should().BeTrue();
        res.Data!.IsBootstrap.Should().BeTrue();
        res.Data.Session.Should().NotBeNull();
        res.Data.Session!.Channel.Should().Be(nameof(ProductChannel.Corporate));
        res.Data.Session.CompanyId.Should().BeNull();
        res.Data.Session.IsBootstrap.Should().BeTrue();
        user.SessionChannel.Should().Be(ProductChannel.Corporate);
        user.SessionCompanyId.Should().BeNull();
        _tokens.Verify(t => t.GenerateAccessToken(user, ProductChannel.Corporate, null, null), Times.Once);
    }

    [Fact]
    public async Task CorporateLogin_SingleMembership_AutoBindsCompanyAndRole()
    {
        var user = ActiveUser();
        var companyId = Guid.NewGuid();
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _memberships.Setup(m => m.GetActiveMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CompanyMembershipSummary(companyId, "Acme", "Admin") });

        var handler = new CorporateLoginHandler(_uow.Object, _tokens.Object, _hasher.Object, _memberships.Object, _loginLog.Object);
        var res = await handler.HandleAsync(new CorporateLoginCommand(new CorporateLoginDto(user.Email!, "secret")));

        res.Success.Should().BeTrue();
        res.Data!.IsBootstrap.Should().BeFalse();
        res.Data.Session!.CompanyId.Should().Be(companyId);
        res.Data.Session.CompanyRole.Should().Be("Admin");
        user.SessionCompanyId.Should().Be(companyId);
        user.SessionCompanyRole.Should().Be("Admin");
        _tokens.Verify(t => t.GenerateAccessToken(user, ProductChannel.Corporate, companyId, "Admin"), Times.Once);
    }

    [Fact]
    public async Task CorporateLogin_MultipleMemberships_WithoutCompanyId_RequiresSelection()
    {
        var user = ActiveUser();
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _memberships.Setup(m => m.GetActiveMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CompanyMembershipSummary(Guid.NewGuid(), "A", "Admin"),
                new CompanyMembershipSummary(Guid.NewGuid(), "B", "Employee")
            });

        var handler = new CorporateLoginHandler(_uow.Object, _tokens.Object, _hasher.Object, _memberships.Object, _loginLog.Object);
        var res = await handler.HandleAsync(new CorporateLoginCommand(new CorporateLoginDto(user.Email!, "secret")));

        res.Success.Should().BeFalse();
        res.Code.Should().Be("company_selection_required");
        res.Data!.RequiresCompanySelection.Should().BeTrue();
        res.Data.Memberships.Should().HaveCount(2);
        res.Data.Session.Should().BeNull();
        _tokens.Verify(t => t.GenerateAccessToken(It.IsAny<User>(), It.IsAny<ProductChannel>(), It.IsAny<Guid?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task SwitchChannel_BootstrapToCompany_BindsAdminMembership()
    {
        var user = ActiveUser();
        var companyId = Guid.NewGuid();
        user.BindSession(ProductChannel.Corporate); // bootstrap
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _memberships.Setup(m => m.GetActiveMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CompanyMembershipSummary(companyId, "Acme", "Admin") });

        var handler = new SwitchChannelHandler(_uow.Object, _tokens.Object, _memberships.Object, _switchLog.Object);
        var res = await handler.HandleAsync(new SwitchChannelCommand(user.Id, new SwitchChannelDto("Corporate", companyId)));

        res.Success.Should().BeTrue();
        res.Data!.CompanyId.Should().Be(companyId);
        res.Data.CompanyRole.Should().Be("Admin");
        res.Data.IsBootstrap.Should().BeFalse();
        user.SessionCompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task SwitchChannel_CorporateToMarketplace_ClearsCompany()
    {
        var user = ActiveUser();
        var companyId = Guid.NewGuid();
        user.BindSession(ProductChannel.Corporate, companyId, "Employee");
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var handler = new SwitchChannelHandler(_uow.Object, _tokens.Object, _memberships.Object, _switchLog.Object);
        var res = await handler.HandleAsync(new SwitchChannelCommand(user.Id, new SwitchChannelDto("Marketplace")));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Marketplace));
        res.Data.CompanyId.Should().BeNull();
        user.SessionChannel.Should().Be(ProductChannel.Marketplace);
        user.SessionCompanyId.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_CorporateChannelWithCompanyId_ValidatesMembership()
    {
        var user = ActiveUser();
        user.RotateRefreshToken("rt", DateTime.UtcNow.AddDays(1));
        user.BindSession(ProductChannel.Marketplace);
        var companyId = Guid.NewGuid();
        _users.Setup(r => r.GetByRefreshTokenAsync("rt", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _tokens.Setup(t => t.ValidateRefreshToken(user, "rt")).Returns(true);
        _memberships.Setup(m => m.GetActiveMembershipsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CompanyMembershipSummary(companyId, "Acme", "Employee") });

        var handler = new RefreshTokenHandler(_uow.Object, _tokens.Object, _memberships.Object);
        var res = await handler.HandleAsync(new RefreshTokenCommand(new RefreshTokenDto("rt", "Corporate", companyId)));

        res.Success.Should().BeTrue();
        res.Data!.Channel.Should().Be(nameof(ProductChannel.Corporate));
        res.Data.CompanyId.Should().Be(companyId);
        res.Data.CompanyRole.Should().Be("Employee");
        _tokens.Verify(t => t.GenerateAccessToken(user, ProductChannel.Corporate, companyId, "Employee"), Times.Once);
    }

    [Fact]
    public async Task GetChannelContext_IncludesIsolationFlagAndMemberships()
    {
        var userId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        _memberships.Setup(m => m.GetActiveMembershipsAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CompanyMembershipSummary(companyId, "Acme", "Admin") });

        var handler = new GetChannelContextHandler(_memberships.Object);
        var res = await handler.HandleAsync(new GetChannelContextQuery(
            userId, "Corporate", companyId, "Admin", IsolationEnabled: true));

        res.Success.Should().BeTrue();
        res.Data!.IsolationEnabled.Should().BeTrue();
        res.Data.IsBootstrap.Should().BeFalse();
        res.Data.Memberships.Should().ContainSingle(m => m.CompanyId == companyId && m.Role == "Admin");
    }
}
