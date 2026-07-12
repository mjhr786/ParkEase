using FluentAssertions;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.Corporate;

public class CorporateInvoiceDomainTests
{
    [Fact]
    public void Create_ShouldSumLineAmounts_AndAllowZeroTotal()
    {
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Guid.NewGuid(),
            Array.Empty<CorporateInvoiceLineDraft>());

        invoice.Status.Should().Be(CorporateInvoiceStatus.Draft);
        invoice.Subtotal.Should().Be(0);
        invoice.TotalAmount.Should().Be(0);
        invoice.InvoiceNumber.Should().StartWith("CINV-");
    }

    [Fact]
    public void Issue_MarkPaid_AndVoid_ShouldEnforceLifecycle()
    {
        var admin = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.UsageBased,
            new DateOnly(2026, 5, 1),
            new DateOnly(2026, 5, 31),
            admin,
            new[]
            {
                new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, "Usage — A", 1, 100m)
            });

        invoice.Issue(admin);
        invoice.Status.Should().Be(CorporateInvoiceStatus.Issued);
        invoice.IssuedAt.Should().NotBeNull();

        Action issueAgain = () => invoice.Issue(admin);
        issueAgain.Should().Throw<InvalidOperationException>();

        invoice.MarkPaid(admin, "NEFT-1", "June settlement");
        invoice.Status.Should().Be(CorporateInvoiceStatus.Paid);
        invoice.PaymentReference.Should().Be("NEFT-1");

        Action voidPaid = () => invoice.Void(admin, "oops");
        voidPaid.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Void_Draft_ShouldSucceed()
    {
        var admin = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.ReservedSlots,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 4, 30),
            admin,
            Array.Empty<CorporateInvoiceLineDraft>());

        invoice.Void(admin, "Wrong period");
        invoice.Status.Should().Be(CorporateInvoiceStatus.Void);
        invoice.VoidReason.Should().Be("Wrong period");
    }

    [Fact]
    public void ValidatePeriod_ShouldRejectTooLongSpan()
    {
        Action act = () => CorporateInvoice.ValidatePeriod(
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 4, 5));

        act.Should().Throw<ArgumentException>();
    }
}

public class CorporateInvoiceCalculatorTests
{
    private readonly CorporateInvoiceCalculator _sut = new();

    [Fact]
    public void ReservedSlots_ShouldProrateVendorLease_AndExcludeOwned()
    {
        var periodStart = new DateOnly(2026, 6, 1);
        var periodEnd = new DateOnly(2026, 6, 30);
        var allocations = new[]
        {
            new InvoiceAllocationChargeInput(
                Guid.NewGuid(),
                "Vendor Lot",
                "LEASE-1",
                ParkingAllocationSource.VendorLease,
                AllocationStatus.Active,
                3000m,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)),
            new InvoiceAllocationChargeInput(
                Guid.NewGuid(),
                "Owned Lot",
                null,
                ParkingAllocationSource.CompanyOwned,
                AllocationStatus.Active,
                9999m,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31)),
            new InvoiceAllocationChargeInput(
                Guid.NewGuid(),
                "Pending Lease",
                null,
                ParkingAllocationSource.VendorLease,
                AllocationStatus.PendingApproval,
                1000m,
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31))
        };

        var lines = _sut.BuildLines(
            BillingType.ReservedSlots,
            periodStart,
            periodEnd,
            allocations,
            Array.Empty<InvoiceBookingChargeInput>());

        lines.Should().HaveCount(1);
        lines[0].LineType.Should().Be(CorporateInvoiceLineType.ReservedCapacity);
        lines[0].Quantity.Should().Be(1m);
        lines[0].UnitAmount.Should().Be(3000m);
        lines[0].Description.Should().Contain("Vendor Lot");
        lines[0].Description.Should().Contain("30d");
    }

    [Fact]
    public void ReservedSlots_PartialOverlap_ShouldProrateByDays()
    {
        // Period June (30d); contract only first 10 days
        var lines = _sut.BuildLines(
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new[]
            {
                new InvoiceAllocationChargeInput(
                    Guid.NewGuid(),
                    "Partial",
                    null,
                    ParkingAllocationSource.VendorLease,
                    AllocationStatus.Active,
                    3000m,
                    new DateOnly(2026, 6, 1),
                    new DateOnly(2026, 6, 10))
            },
            Array.Empty<InvoiceBookingChargeInput>());

        lines.Should().HaveCount(1);
        lines[0].Quantity.Should().Be(Math.Round(10m / 30m, 4, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void UsageBased_ShouldIncludePositiveNonCancelledBookingsInPeriod()
    {
        var bookingId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var bookings = new[]
        {
            new InvoiceBookingChargeInput(
                bookingId,
                allocationId,
                150m,
                new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc),
                BookingStatus.Confirmed,
                false,
                "Jane Doe",
                null,
                "HQ Lot"),
            new InvoiceBookingChargeInput(
                Guid.NewGuid(),
                allocationId,
                80m,
                new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 12, 11, 0, 0, DateTimeKind.Utc),
                BookingStatus.Cancelled,
                false,
                "Jane Doe",
                null,
                "HQ Lot"),
            new InvoiceBookingChargeInput(
                Guid.NewGuid(),
                allocationId,
                200m,
                new DateTime(2026, 5, 31, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 31, 11, 0, 0, DateTimeKind.Utc),
                BookingStatus.Confirmed,
                false,
                "Out of period",
                null,
                "HQ Lot")
        };

        var lines = _sut.BuildLines(
            BillingType.UsageBased,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Array.Empty<InvoiceAllocationChargeInput>(),
            bookings);

        lines.Should().HaveCount(1);
        lines[0].LineType.Should().Be(CorporateInvoiceLineType.Usage);
        lines[0].UnitAmount.Should().Be(150m);
        lines[0].BookingId.Should().Be(bookingId);
        lines[0].Description.Should().Contain("Jane Doe");
    }
}
