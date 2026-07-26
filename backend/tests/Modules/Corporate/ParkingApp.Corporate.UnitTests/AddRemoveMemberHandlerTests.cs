using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Members;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Corporate.UnitTests;

public class AddRemoveMemberHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public AddRemoveMemberHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private Company CreateCompany() =>
        Company.Create("Acme", "REG-M", "a@acme.com", "555", "Addr", BillingType.UsageBased, _adminId);

    [Fact]
    public async Task AddMember_WhenUserMissing_ReturnsFailure()
    {
        _users.Setup(x => x.GetByEmailAsync("x@y.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSummary?)null);

        var handler = new AddMemberHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AddMemberCommand(
            Guid.NewGuid(), _adminId, new AddMemberDto("x@y.com")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("invite");
    }

    [Fact]
    public async Task AddMember_WhenCompanyMissing_ReturnsNotFound()
    {
        _users.Setup(x => x.GetByEmailAsync("e@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(Guid.NewGuid(), "e@acme.com", "Emp", "One"));
        _companies.Setup(x => x.GetWithMembershipsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new AddMemberHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AddMemberCommand(
            Guid.NewGuid(), _adminId, new AddMemberDto("e@acme.com")));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task AddMember_WhenAdmin_Succeeds()
    {
        var company = CreateCompany();
        var empId = Guid.NewGuid();
        _users.Setup(x => x.GetByEmailAsync("e@acme.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(empId, "e@acme.com", "Emp", "One"));
        _companies.Setup(x => x.GetWithMembershipsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new AddMemberHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new AddMemberCommand(
            company.Id, _adminId, new AddMemberDto("e@acme.com", CompanyRole.Employee, "E-1", 2)));

        result.Success.Should().BeTrue();
        result.Data!.UserId.Should().Be(empId);
        result.Data.EmployeeCode.Should().Be("E-1");
    }

    [Fact]
    public async Task RemoveMember_WhenNotLastAdmin_Succeeds()
    {
        var company = CreateCompany();
        var secondAdminId = Guid.NewGuid();
        company.AddMember(_adminId, secondAdminId, CompanyRole.Admin);
        var empId = Guid.NewGuid();
        var emp = company.AddMember(_adminId, empId, CompanyRole.Employee);

        _companies.Setup(x => x.GetWithMembershipsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new RemoveMemberHandler(_uow.Object);
        var result = await handler.HandleAsync(new RemoveMemberCommand(
            company.Id, emp.Id, _adminId));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMember_WhenLastAdmin_ReturnsFailure()
    {
        var company = CreateCompany();
        var adminMembership = company.Memberships.Single(m => m.UserId == _adminId);
        _companies.Setup(x => x.GetWithMembershipsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new RemoveMemberHandler(_uow.Object);
        var result = await handler.HandleAsync(new RemoveMemberCommand(
            company.Id, adminMembership.Id, _adminId));

        result.Success.Should().BeFalse();
    }
}
