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

namespace ParkingApp.UnitTests.Handlers;

public class InviteMemberHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IEmployeeInvitationRepository> _invites = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ICorporateWebLinkBuilder> _links = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public InviteMemberHandlerTests()
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
        Company.Create(
            "Acme Corp", "REG-100", "admin@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);

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
    }
}
