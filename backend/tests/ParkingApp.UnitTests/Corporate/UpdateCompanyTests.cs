using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Companies;
using ParkingApp.Application.CQRS.Commands.Corporate.Members;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

public class UpdateCompanyTests
{
    [Fact]
    public void Company_UpdateProfile_ShouldUpdateDetailsAndBilling()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);

        company.UpdateProfile(
            adminId,
            "Acme Parking",
            "ops@acme.com",
            "1111111111",
            "New Addr",
            BillingType.UsageBased);

        company.Name.Should().Be("Acme Parking");
        company.ContactEmail.Should().Be("ops@acme.com");
        company.ContactPhone.Should().Be("1111111111");
        company.BillingAddress.Should().Be("New Addr");
        company.BillingType.Should().Be(BillingType.UsageBased);
    }

    [Fact]
    public void Company_CancelInvitation_ShouldMarkCancelled()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var invitation = company.InviteMember(adminId, "new@acme.com", CompanyRole.Employee);

        company.CancelInvitation(adminId, invitation.Id);

        invitation.Status.Should().Be(InvitationStatus.Cancelled);
    }

    [Fact]
    public async Task UpdateCompanyHandler_WhenNoFields_Fails()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);

        var uow = new Mock<ICorporateUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        uow.Setup(u => u.Companies).Returns(companies.Object);
        companies.Setup(c => c.GetWithMembershipsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new UpdateCompanyHandler(uow.Object);
        var result = await handler.HandleAsync(new UpdateCompanyCommand(companyId, adminId, new UpdateCompanyDto()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No company fields");
    }

    [Fact]
    public async Task CancelInvitationHandler_WhenFound_Cancels()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var invitation = company.InviteMember(adminId, "new@acme.com", CompanyRole.Employee);

        var uow = new Mock<ICorporateUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        uow.Setup(u => u.Companies).Returns(companies.Object);
        companies.Setup(c => c.GetFullAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new CancelInvitationHandler(uow.Object);
        var result = await handler.HandleAsync(new CancelInvitationCommand(company.Id, adminId, invitation.Id));

        result.Success.Should().BeTrue();
        invitation.Status.Should().Be(InvitationStatus.Cancelled);
    }

    [Fact]
    public void Company_ResendInvitation_ShouldIssueNewTokenAndPendingStatus()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var invitation = company.InviteMember(adminId, "new@acme.com", CompanyRole.Employee);
        var oldToken = invitation.InvitationToken;
        invitation.MarkExpired();

        var renewed = company.ResendInvitation(adminId, invitation.Id);

        renewed.Status.Should().Be(InvitationStatus.Pending);
        renewed.InvitationToken.Should().NotBe(oldToken);
        renewed.ExpiresAt.Should().BeAfter(DateTime.UtcNow.AddDays(6));
    }
}
