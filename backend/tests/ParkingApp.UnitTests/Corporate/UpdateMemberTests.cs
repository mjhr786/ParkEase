using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Members;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Corporate;

public class UpdateMemberTests
{
    [Fact]
    public void Company_UpdateMember_ShouldChangeRoleAndPriority()
    {
        var adminId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var membership = company.AddMember(adminId, employeeId, CompanyRole.Employee, "E-1", priority: 2);

        company.UpdateMember(adminId, membership.Id, role: CompanyRole.Admin, priority: 8, employeeCode: "E-99", updateEmployeeCode: true);

        membership.Role.Should().Be(CompanyRole.Admin);
        membership.Priority.Should().Be(8);
        membership.EmployeeCode.Should().Be("E-99");
    }

    [Fact]
    public void Company_UpdateMember_WhenDemotingLastAdmin_ShouldThrow()
    {
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);
        var adminMembership = company.Memberships.First(m => m.UserId == adminId);

        var act = () => company.UpdateMember(adminId, adminMembership.Id, role: CompanyRole.Employee);

        act.Should().Throw<InvalidOperationException>().WithMessage("*last admin*");
    }

    [Fact]
    public async Task Handler_WhenNoFields_ReturnsFailure()
    {
        var companyId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var company = Company.Create("Acme", "REG", "a@b.com", "9999999999", "Addr", BillingType.ReservedSlots, adminId);

        var uow = new Mock<ICorporateUnitOfWork>();
        var companies = new Mock<ICompanyRepository>();
        uow.Setup(u => u.Companies).Returns(companies.Object);
        companies.Setup(c => c.GetWithMembershipsAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new UpdateMemberHandler(uow.Object);
        var result = await handler.HandleAsync(new UpdateMemberCommand(
            companyId,
            Guid.NewGuid(),
            adminId,
            new UpdateMemberDto()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No member fields");
    }
}
