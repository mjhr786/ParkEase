using FluentAssertions;
using Moq;
using ParkingApp.Application.CQRS.Commands.Corporate;
using ParkingApp.Application.CQRS.Commands.Corporate.Invoices;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Corporate.UnitTests;

public class CorporateInvoiceHandlerTests
{
    private readonly Mock<ICorporateUnitOfWork> _uow = new();
    private readonly Mock<ICompanyRepository> _companies = new();
    private readonly Mock<ICorporateInvoiceRepository> _invoices = new();
    private readonly Mock<ICorporateBookingRepository> _bookings = new();
    private readonly Mock<ICorporateInvoiceCalculator> _calculator = new();
    private readonly Guid _adminId = Guid.NewGuid();

    public CorporateInvoiceHandlerTests()
    {
        _uow.Setup(x => x.Companies).Returns(_companies.Object);
        _uow.Setup(x => x.Invoices).Returns(_invoices.Object);
        _uow.Setup(x => x.CorporateBookings).Returns(_bookings.Object);
        _uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _invoices.Setup(x => x.AddAsync(It.IsAny<CorporateInvoice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorporateInvoice i, CancellationToken _) => i);
    }

    private Company CreateCompany(BillingType billing = BillingType.ReservedSlots) =>
        Company.Create("Acme", "REG-INV", "a@acme.com", "555", "Addr", billing, _adminId);

    [Fact]
    public async Task Generate_WhenInvalidPeriod_ReturnsFailure()
    {
        var handler = new GenerateCorporateInvoiceHandler(_uow.Object, _calculator.Object);
        var result = await handler.HandleAsync(new GenerateCorporateInvoiceCommand(
            Guid.NewGuid(),
            _adminId,
            new GenerateCorporateInvoiceDto(new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1))));

        result.Success.Should().BeFalse();
        result.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Generate_WhenCompanyMissing_ReturnsNotFound()
    {
        _companies.Setup(x => x.GetWithAllocationsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        var handler = new GenerateCorporateInvoiceHandler(_uow.Object, _calculator.Object);
        var result = await handler.HandleAsync(new GenerateCorporateInvoiceCommand(
            Guid.NewGuid(),
            _adminId,
            new GenerateCorporateInvoiceDto(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Generate_WhenNotAdmin_ReturnsFailure()
    {
        var company = CreateCompany();
        var employeeId = Guid.NewGuid();
        company.AddMember(_adminId, employeeId, CompanyRole.Employee);
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        var handler = new GenerateCorporateInvoiceHandler(_uow.Object, _calculator.Object);
        var result = await handler.HandleAsync(new GenerateCorporateInvoiceCommand(
            company.Id,
            employeeId,
            new GenerateCorporateInvoiceDto(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task Generate_WhenDuplicatePeriod_ReturnsFailure()
    {
        var company = CreateCompany();
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _invoices.Setup(x => x.ExistsNonVoidForPeriodAsync(
                company.Id, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new GenerateCorporateInvoiceHandler(_uow.Object, _calculator.Object);
        var result = await handler.HandleAsync(new GenerateCorporateInvoiceCommand(
            company.Id,
            _adminId,
            new GenerateCorporateInvoiceDto(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task Generate_WhenAdmin_CreatesDraft()
    {
        var company = CreateCompany();
        _companies.Setup(x => x.GetWithAllocationsAsync(company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);
        _invoices.Setup(x => x.ExistsNonVoidForPeriodAsync(
                company.Id, It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _calculator.Setup(x => x.BuildLines(
                BillingType.ReservedSlots,
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<IReadOnlyList<InvoiceAllocationChargeInput>>(),
                It.IsAny<IReadOnlyList<InvoiceBookingChargeInput>>()))
            .Returns(new[]
            {
                new CorporateInvoiceLineDraft(CorporateInvoiceLineType.ReservedCapacity, "Capacity", 1m, 1000m)
            });

        var handler = new GenerateCorporateInvoiceHandler(_uow.Object, _calculator.Object);
        var result = await handler.HandleAsync(new GenerateCorporateInvoiceCommand(
            company.Id,
            _adminId,
            new GenerateCorporateInvoiceDto(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30))));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Status.Should().Be(CorporateInvoiceStatus.Draft);
        _invoices.Verify(x => x.AddAsync(It.IsAny<CorporateInvoice>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Issue_WhenNotAdmin_ReturnsFailure()
    {
        _companies.Setup(x => x.GetMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(Guid.NewGuid(), Guid.NewGuid(), CompanyRole.Employee));

        var handler = new IssueCorporateInvoiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new IssueCorporateInvoiceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("admins");
    }

    [Fact]
    public async Task Issue_WhenMissing_ReturnsNotFound()
    {
        var companyId = Guid.NewGuid();
        _companies.Setup(x => x.GetMembershipAsync(companyId, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, _adminId, CompanyRole.Admin));
        _invoices.Setup(x => x.GetByIdWithLinesAsync(companyId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CorporateInvoice?)null);

        var handler = new IssueCorporateInvoiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new IssueCorporateInvoiceCommand(
            companyId, _adminId, Guid.NewGuid()));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task Issue_WhenDraft_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            companyId, BillingType.UsageBased,
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31),
            _adminId,
            new[] { new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, "Usage", 1, 50m) });

        _companies.Setup(x => x.GetMembershipAsync(companyId, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, _adminId, CompanyRole.Admin));
        _invoices.Setup(x => x.GetByIdWithLinesAsync(companyId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var handler = new IssueCorporateInvoiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new IssueCorporateInvoiceCommand(companyId, _adminId, invoice.Id));

        result.Success.Should().BeTrue();
        invoice.Status.Should().Be(CorporateInvoiceStatus.Issued);
    }

    [Fact]
    public async Task MarkPaid_WhenIssued_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            companyId, BillingType.UsageBased,
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31),
            _adminId,
            new[] { new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, "Usage", 1, 50m) });
        invoice.Issue(_adminId);

        _companies.Setup(x => x.GetMembershipAsync(companyId, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, _adminId, CompanyRole.Admin));
        _invoices.Setup(x => x.GetByIdWithLinesAsync(companyId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var handler = new MarkCorporateInvoicePaidHandler(_uow.Object);
        var result = await handler.HandleAsync(new MarkCorporateInvoicePaidCommand(
            companyId, _adminId, invoice.Id, new MarkInvoicePaidDto("NEFT-1", "ok")));

        result.Success.Should().BeTrue();
        invoice.Status.Should().Be(CorporateInvoiceStatus.Paid);
        invoice.PaymentReference.Should().Be("NEFT-1");
    }

    [Fact]
    public async Task Void_WhenDraft_Succeeds()
    {
        var companyId = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            companyId, BillingType.ReservedSlots,
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            _adminId, Array.Empty<CorporateInvoiceLineDraft>());

        _companies.Setup(x => x.GetMembershipAsync(companyId, _adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserCompanyMembership.Create(companyId, _adminId, CompanyRole.Admin));
        _invoices.Setup(x => x.GetByIdWithLinesAsync(companyId, invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);

        var handler = new VoidCorporateInvoiceHandler(_uow.Object);
        var result = await handler.HandleAsync(new VoidCorporateInvoiceCommand(
            companyId, _adminId, invoice.Id, new VoidInvoiceDto("Wrong period")));

        result.Success.Should().BeTrue();
        invoice.Status.Should().Be(CorporateInvoiceStatus.Void);
    }
}
