using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Companies;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.Corporate.UnitTests;

public class CreateCompanyHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Guid _userId = Guid.NewGuid();

    public CreateCompanyHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _companies.Setup(x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken _) => c);
    }

    [Fact]
    public async Task Create_WhenUserMissing_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSummary?)null);

        var handler = new CreateCompanyHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new CreateCompanyCommand(
            _userId,
            new CreateCompanyDto("Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.UsageBased)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task Create_WhenRegistrationExists_ReturnsFailure()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "u@x.com", "U", "X"));
        _companies.Setup(x => x.ExistsByRegistrationNumberAsync("REG-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new CreateCompanyHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new CreateCompanyCommand(
            _userId,
            new CreateCompanyDto("Acme", "REG-1", "a@acme.com", "555", "Addr", BillingType.ReservedSlots)));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("registration number");
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCompanyDto()
    {
        _users.Setup(x => x.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "u@x.com", "U", "X"));
        _companies.Setup(x => x.ExistsByRegistrationNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new CreateCompanyHandler(_uow.Object, _users.Object);
        var result = await handler.HandleAsync(new CreateCompanyCommand(
            _userId,
            new CreateCompanyDto("Acme Corp", "REG-99", "a@acme.com", "555", "Billing Addr", BillingType.UsageBased)));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Name.Should().Be("Acme Corp");
        result.Data.RegistrationNumber.Should().Be("REG-99");
        result.Data.MemberCount.Should().Be(1);
        _companies.Verify(x => x.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
