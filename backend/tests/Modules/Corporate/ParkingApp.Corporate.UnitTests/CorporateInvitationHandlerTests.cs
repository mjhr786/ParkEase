using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Members;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Corporate.UnitTests;

/// <summary>Wave 16: Invite / accept / resend / cancel handlers in the Corporate module suite.</summary>
public class CorporateInvitationHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IEmployeeInvitationRepository> _invites = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ICorporateWebLinkBuilder> _links = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public CorporateInvitationHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.EmployeeInvitations).Returns(_invites.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _invites.Setup(x => x.AddAsync(It.IsAny<EmployeeInvitation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EmployeeInvitation i, CancellationToken _) => i);
        _email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        _links.Setup(x => x.BuildInviteAcceptUrl(It.IsAny<string>())).Returns("https://app/invite/token");
    }

    private Company CreateCompany() =>
        Company.Create("Acme Corp", "REG-INV", "admin@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);

    private (Company company, EmployeeInvitation invitation) CreateCompanyWithInvite(string email = "join@acme.com")
    {
        var company = CreateCompany();
        var invitation = company.InviteMember(_adminId, email, CompanyRole.Employee);
        return (company, invitation);
    }

    // ── Invite ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Invite_WhenCompanyMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        _companies.Setup(x => x.GetByIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            companyId, _adminId, new InviteMemberDto("new@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Invite_WhenCompanyInactive_ReturnsFailure()
    {
        var company = CreateCompany();
        company.Deactivate();
        _companies.Setup(x => x.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            company.Id, _adminId, new InviteMemberDto("new@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("inactive");
    }

    [Fact]
    public async Task Invite_WhenNotAdmin_ReturnsFailure()
    {
        var company = CreateCompany();
        _companies.Setup(x => x.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(company.Id, _adminId, CompanyRole.Employee));

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            company.Id, _adminId, new InviteMemberDto("new@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task Invite_WhenAlreadyMember_ReturnsFailure()
    {
        var company = CreateCompany();
        var existingUserId = Guid.NewGuid();
        _companies.Setup(x => x.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(company.Id, _adminId, CompanyRole.Admin));
        _users.Setup(x => x.GetByEmailAsync("member@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(existingUserId, "member@acme.com", "Mem", "Ber"));
        _companies.Setup(x => x.IsUserMemberAsync(company.Id, existingUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            company.Id, _adminId, new InviteMemberDto("member@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already a member");
    }

    [Fact]
    public async Task Invite_WhenPendingExists_ReturnsFailure()
    {
        var company = CreateCompany();
        _companies.Setup(x => x.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(company.Id, _adminId, CompanyRole.Admin));
        _users.Setup(x => x.GetByEmailAsync("new@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSummary?)null);
        _invites.Setup(x => x.HasPendingInvitationAsync(company.Id, "new@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            company.Id, _adminId, new InviteMemberDto("new@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("pending invitation");
    }

    [Fact]
    public async Task Invite_WhenAdmin_CreatesInvitation()
    {
        var company = CreateCompany();
        _companies.Setup(x => x.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        _companies.Setup(x => x.GetMembershipAsync(company.Id, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(company.Id, _adminId, CompanyRole.Admin));
        _users.Setup(x => x.GetByEmailAsync("new@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSummary?)null);
        _invites.Setup(x => x.HasPendingInvitationAsync(company.Id, "new@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new InviteMemberHandler(
            _uow.Object, _users.Object, _email.Object, _links.Object, NullLogger<InviteMemberHandler>.Instance);

        var result = await handler.HandleAsync(new InviteMemberCommand(
            company.Id, _adminId, new InviteMemberDto("new@acme.com", CompanyRole.Employee)));

        result.Success.Should().BeTrue();
        result.Data!.Email.Should().Be("new@acme.com");
        _invites.Verify(x => x.AddAsync(It.IsAny<EmployeeInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Accept ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Accept_WhenUserMissing_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSummary?)null);

        var handler = new AcceptInvitationHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AcceptInvitationCommand(Guid.NewGuid(), "token"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task Accept_WhenInvalidToken_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "join@acme.com", "Jo", "In"));
        _companies.Setup(x => x.GetAggregateForInvitationAcceptanceAsync("bad", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new AcceptInvitationHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AcceptInvitationCommand(userId, "bad"));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Invalid or expired");
    }

    [Fact]
    public async Task Accept_WhenValid_CreatesMembership()
    {
        var (company, invitation) = CreateCompanyWithInvite("join@acme.com");
        var userId = Guid.NewGuid();
        _users.Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(userId, "join@acme.com", "Jo", "In"));
        _companies.Setup(x => x.GetAggregateForInvitationAcceptanceAsync(
                invitation.InvitationToken, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new AcceptInvitationHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AcceptInvitationCommand(userId, invitation.InvitationToken));

        result.Success.Should().BeTrue();
        result.Data!.UserId.Should().Be(userId);
        result.Data.CompanyId.Should().Be(company.Id);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
    }

    // ── Resend / Cancel ─────────────────────────────────────────────────────

    [Fact]
    public async Task Resend_WhenCompanyMissing_ReturnsFailure()
    {
        _companies.Setup(x => x.GetFullAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new ResendInvitationHandler(
            _uow.Object, _email.Object, _links.Object, NullLogger<ResendInvitationHandler>.Instance);

        var result = await handler.HandleAsync(new ResendInvitationCommand(Guid.NewGuid(), _adminId, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Resend_WhenAdmin_ReturnsNewTokenDto()
    {
        var (company, invitation) = CreateCompanyWithInvite();
        var oldToken = invitation.InvitationToken;
        _companies.Setup(x => x.GetFullAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new ResendInvitationHandler(
            _uow.Object, _email.Object, _links.Object, NullLogger<ResendInvitationHandler>.Instance);

        var result = await handler.HandleAsync(new ResendInvitationCommand(company.Id, _adminId, invitation.Id));

        result.Success.Should().BeTrue();
        result.Data!.InvitationToken.Should().NotBeNullOrWhiteSpace();
        invitation.InvitationToken.Should().NotBe(oldToken);
    }

    [Fact]
    public async Task Cancel_WhenCompanyMissing_ReturnsFailure()
    {
        _companies.Setup(x => x.GetFullAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new CancelInvitationHandler(_uow.Object);
        var result = await handler.HandleAsync(new CancelInvitationCommand(Guid.NewGuid(), _adminId, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Cancel_WhenAdmin_Succeeds()
    {
        var (company, invitation) = CreateCompanyWithInvite();
        _companies.Setup(x => x.GetFullAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new CancelInvitationHandler(_uow.Object);
        var result = await handler.HandleAsync(new CancelInvitationCommand(company.Id, _adminId, invitation.Id));

        result.Success.Should().BeTrue();
        invitation.Status.Should().Be(InvitationStatus.Cancelled);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
