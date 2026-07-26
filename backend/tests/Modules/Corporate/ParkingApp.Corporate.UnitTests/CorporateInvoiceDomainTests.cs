using FluentAssertions;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Application.Services;
using ParkingApp.Corporate.Domain;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.Corporate.UnitTests;

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
                new CorporateInvoiceLineDraft(CorporateInvoiceLineType.Usage, "Usage G�� A", 1, 100m)
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

    [Fact]
    public void ValidatePeriod_ShouldRejectEndBeforeStart()
    {
        Action act = () => CorporateInvoice.ValidatePeriod(
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 1));

        act.Should().Throw<ArgumentException>().WithMessage("*on or after*");
    }

    [Fact]
    public void Create_WithLinesAndTax_SumsTotals()
    {
        var allocationId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.UsageBased,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Guid.NewGuid(),
            new[]
            {
                new CorporateInvoiceLineDraft(
                    CorporateInvoiceLineType.Usage, "  Spot A  ", 2m, 50m, allocationId, bookingId),
                new CorporateInvoiceLineDraft(
                    CorporateInvoiceLineType.ReservedCapacity, "Credit", 1m, 10m)
            },
            currency: "inr",
            taxAmount: 18m);

        invoice.Currency.Should().Be("INR");
        invoice.LineItems.Should().HaveCount(2);
        invoice.Subtotal.Should().Be(110m); // 2*50 + 1*10
        invoice.TaxAmount.Should().Be(18m);
        invoice.TotalAmount.Should().Be(128m);
        invoice.LineItems.First().Description.Should().Be("Spot A");
        invoice.LineItems.First().Amount.Should().Be(100m);
        invoice.LineItems.First().AllocationId.Should().Be(allocationId);
        invoice.LineItems.First().BookingId.Should().Be(bookingId);
    }

    [Fact]
    public void Create_RejectsEmptyCompany_NegativeTax_NullLines()
    {
        Action emptyCompany = () => CorporateInvoice.Create(
            Guid.Empty, BillingType.UsageBased,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            Guid.NewGuid(), Array.Empty<CorporateInvoiceLineDraft>());
        emptyCompany.Should().Throw<ArgumentException>().WithMessage("*Company*");

        Action emptyActor = () => CorporateInvoice.Create(
            Guid.NewGuid(), BillingType.UsageBased,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            Guid.Empty, Array.Empty<CorporateInvoiceLineDraft>());
        emptyActor.Should().Throw<ArgumentException>().WithMessage("*Generated*");

        Action negTax = () => CorporateInvoice.Create(
            Guid.NewGuid(), BillingType.UsageBased,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            Guid.NewGuid(), Array.Empty<CorporateInvoiceLineDraft>(), taxAmount: -1m);
        negTax.Should().Throw<ArgumentOutOfRangeException>();

        Action nullLines = () => CorporateInvoice.Create(
            Guid.NewGuid(), BillingType.UsageBased,
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            Guid.NewGuid(), null!);
        nullLines.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MarkPaid_OnDraft_Throws_And_VoidIssued_Succeeds()
    {
        var admin = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            admin,
            new[] { new CorporateInvoiceLineDraft(CorporateInvoiceLineType.ReservedCapacity, "Lease", 1m, 1000m) });

        Action payDraft = () => invoice.MarkPaid(admin);
        payDraft.Should().Throw<InvalidOperationException>().WithMessage("*issued*");

        invoice.Issue(admin);
        invoice.Void(admin, "customer cancelled");
        invoice.Status.Should().Be(CorporateInvoiceStatus.Void);
        invoice.VoidedByUserId.Should().Be(admin);
        invoice.VoidReason.Should().Be("customer cancelled");

        Action voidAgain = () => invoice.Void(admin, "again");
        voidAgain.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Void_ShortReason_And_EmptyActor_Throw()
    {
        var admin = Guid.NewGuid();
        var invoice = CorporateInvoice.Create(
            Guid.NewGuid(),
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            admin,
            Array.Empty<CorporateInvoiceLineDraft>());

        Action shortReason = () => invoice.Void(admin, "no");
        shortReason.Should().Throw<ArgumentException>().WithMessage("*3 characters*");

        Action emptyActor = () => invoice.Issue(Guid.Empty);
        emptyActor.Should().Throw<ArgumentException>().WithMessage("*User ID*");
    }

    [Fact]
    public void LineItem_Create_ValidatesInputs()
    {
        Action emptyDesc = () => CorporateInvoiceLineItem.Create(
            Guid.NewGuid(), CorporateInvoiceLineType.Usage, "  ", 1, 10);
        emptyDesc.Should().Throw<ArgumentException>();

        Action negQty = () => CorporateInvoiceLineItem.Create(
            Guid.NewGuid(), CorporateInvoiceLineType.Usage, "x", -1, 10);
        negQty.Should().Throw<ArgumentOutOfRangeException>();

        Action negUnit = () => CorporateInvoiceLineItem.Create(
            Guid.NewGuid(), CorporateInvoiceLineType.Usage, "x", 1, -1);
        negUnit.Should().Throw<ArgumentOutOfRangeException>();

        Action emptyInvoice = () => CorporateInvoiceLineItem.Create(
            Guid.Empty, CorporateInvoiceLineType.Usage, "x", 1, 10);
        emptyInvoice.Should().Throw<ArgumentException>();

        var line = CorporateInvoiceLineItem.Create(
            Guid.NewGuid(), CorporateInvoiceLineType.Usage, "  adj  ", 1.5m, 10.555m);
        line.Description.Should().Be("adj");
        line.Amount.Should().Be(15.83m); // 1.5 * 10.56 rounded? unit rounds to 10.56? 
        // UnitAmount = Math.Round(10.555, 2) = 10.56; Quantity = 1.5; amount = Round(1.5*10.555, 2) happens before unit round in code:
        // amount = Round(quantity * unitAmount, 2) with original unitAmount 10.555 => Round(15.8325, 2) = 15.83
        line.UnitAmount.Should().Be(10.56m);
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

    [Fact]
    public void UsageBased_VisitorBooking_UsesVisitorNameInDescription()
    {
        var bookingId = Guid.NewGuid();
        var lines = _sut.BuildLines(
            BillingType.UsageBased,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Array.Empty<InvoiceAllocationChargeInput>(),
            new[]
            {
                new InvoiceBookingChargeInput(
                    bookingId,
                    Guid.NewGuid(),
                    75m,
                    new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 5, 11, 0, 0, DateTimeKind.Utc),
                    BookingStatus.Confirmed,
                    true,
                    null,
                    "Visitor X",
                    "Guest Lot")
            });

        lines.Should().HaveCount(1);
        lines[0].Description.Should().Contain("Visitor X");
        lines[0].UnitAmount.Should().Be(75m);
    }

    [Fact]
    public void ReservedSlots_NoActiveVendorLease_ReturnsEmpty()
    {
        var lines = _sut.BuildLines(
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new[]
            {
                new InvoiceAllocationChargeInput(
                    Guid.NewGuid(),
                    "Owned",
                    null,
                    ParkingAllocationSource.CompanyOwned,
                    AllocationStatus.Active,
                    1000m,
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31))
            },
            Array.Empty<InvoiceBookingChargeInput>());

        lines.Should().BeEmpty();
    }

    [Fact]
    public void UsageBased_ZeroAmountBooking_Excluded()
    {
        var lines = _sut.BuildLines(
            BillingType.UsageBased,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Array.Empty<InvoiceAllocationChargeInput>(),
            new[]
            {
                new InvoiceBookingChargeInput(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    0m,
                    new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 6, 5, 11, 0, 0, DateTimeKind.Utc),
                    BookingStatus.Confirmed,
                    false,
                    "Free",
                    null,
                    "Lot")
            });

        lines.Should().BeEmpty();
    }

    [Fact]
    public void UnsupportedBillingType_Throws()
    {
        var act = () => _sut.BuildLines(
            (BillingType)999,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Array.Empty<InvoiceAllocationChargeInput>(),
            Array.Empty<InvoiceBookingChargeInput>());

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("billingType");
    }

    [Fact]
    public void ReservedSlots_ZeroMonthlyRate_OrNoOverlap_Skipped()
    {
        var lines = _sut.BuildLines(
            BillingType.ReservedSlots,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new[]
            {
                new InvoiceAllocationChargeInput(
                    Guid.NewGuid(),
                    "Zero rate",
                    null,
                    ParkingAllocationSource.VendorLease,
                    AllocationStatus.Active,
                    0m,
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31)),
                new InvoiceAllocationChargeInput(
                    Guid.NewGuid(),
                    "Outside period",
                    null,
                    ParkingAllocationSource.VendorLease,
                    AllocationStatus.Expired,
                    2000m,
                    new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 12, 31))
            },
            Array.Empty<InvoiceBookingChargeInput>());

        lines.Should().BeEmpty();
    }

    [Fact]
    public void UsageBased_ExceedsMaxLineItems_Throws()
    {
        var bookings = Enumerable.Range(0, CorporateInvoice.MaxLineItems + 1)
            .Select(i => new InvoiceBookingChargeInput(
                Guid.NewGuid(),
                Guid.NewGuid(),
                10m,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i),
                new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc).AddHours(i),
                BookingStatus.Confirmed,
                false,
                $"M{i}",
                null,
                "Lot"))
            .ToArray();

        var act = () => _sut.BuildLines(
            BillingType.UsageBased,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            Array.Empty<InvoiceAllocationChargeInput>(),
            bookings);

        act.Should().Throw<InvalidOperationException>().WithMessage("*line items*");
    }
}





