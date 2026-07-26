using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Members;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.UnitTests.Handlers;

public class CorporateInvitationHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ICorporateWebLinkBuilder> _links = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public CorporateInvitationHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _email.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Returns(Task.CompletedTask);
        _links.Setup(x => x.BuildInviteAcceptUrl(It.IsAny<string>())).Returns("https://app/invite/t");
    }

    private (Company company, EmployeeInvitation invitation) CreateCompanyWithInvite(string email = "join@acme.com")
    {
        var company = Company.Create(
            "Acme", "REG-200", "admin@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);
        var invitation = company.InviteMember(_adminId, email, CompanyRole.Employee);
        return (company, invitation);
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

    [Fact]
    public async Task Resend_WhenInvitationMissing_ReturnsFailure()
    {
        var (company, _) = CreateCompanyWithInvite();
        _companies.Setup(x => x.GetFullAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = new ResendInvitationHandler(
            _uow.Object, _email.Object, _links.Object, NullLogger<ResendInvitationHandler>.Instance);

        var result = await handler.HandleAsync(new ResendInvitationCommand(company.Id, _adminId, Guid.NewGuid()));

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
        // Resend typically rotates token
        invitation.InvitationToken.Should().NotBe(oldToken);
    }

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
}
