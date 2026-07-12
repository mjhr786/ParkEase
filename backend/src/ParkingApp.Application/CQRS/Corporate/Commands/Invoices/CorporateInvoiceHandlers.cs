using ParkingApp.Application.CQRS.Commands.Corporate.Shared;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Commands.Corporate.Invoices;

public class GenerateCorporateInvoiceHandler
    : ICommandHandler<GenerateCorporateInvoiceCommand, ApiResponse<CorporateInvoiceDetailDto>>
{
    private readonly ICorporateUnitOfWork _uow;
    private readonly ICorporateInvoiceCalculator _calculator;

    public GenerateCorporateInvoiceHandler(ICorporateUnitOfWork uow, ICorporateInvoiceCalculator calculator)
    {
        _uow = uow;
        _calculator = calculator;
    }

    public async Task<ApiResponse<CorporateInvoiceDetailDto>> HandleAsync(
        GenerateCorporateInvoiceCommand command,
        CancellationToken ct = default)
    {
        var periodStart = command.Dto.PeriodStart;
        var periodEnd = command.Dto.PeriodEnd;

        try
        {
            CorporateInvoice.ValidatePeriod(periodStart, periodEnd);
        }
        catch (ArgumentException ex)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }

        var company = await _uow.Companies.GetWithAllocationsAsync(command.CompanyId, ct);
        if (company == null)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Company not found.", null);
        }

        var membership = company.Memberships.FirstOrDefault(m => m.UserId == command.AdminUserId && !m.IsDeleted);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Only company admins can generate invoices.", null);
        }

        if (!company.IsActive)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Company is inactive.", null);
        }

        if (await _uow.Invoices.ExistsNonVoidForPeriodAsync(command.CompanyId, periodStart, periodEnd, ct))
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(
                false,
                "A non-void invoice already exists for this billing period.",
                null);
        }

        var allocationInputs = company.Allocations
            .Where(a => !a.IsDeleted)
            .Select(a => new InvoiceAllocationChargeInput(
                a.Id,
                a.ParkingSpace?.Title ?? "Parking",
                a.LeaseReference,
                a.SourceType,
                a.Status,
                a.MonthlyRate,
                DateOnly.FromDateTime(a.StartDate),
                DateOnly.FromDateTime(a.EndDate)))
            .ToList();

        IReadOnlyList<InvoiceBookingChargeInput> bookingInputs = Array.Empty<InvoiceBookingChargeInput>();
        if (company.BillingType == BillingType.UsageBased)
        {
            var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var periodEndExclusiveUtc = periodEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            // Fetch one extra row so calculator / handler can reject oversized periods.
            var corporateBookings = await _uow.CorporateBookings.GetBillableBookingsForPeriodAsync(
                command.CompanyId,
                periodStartUtc,
                periodEndExclusiveUtc,
                CorporateInvoice.MaxLineItems + 1,
                ct);

            if (corporateBookings.Count > CorporateInvoice.MaxLineItems)
            {
                return new ApiResponse<CorporateInvoiceDetailDto>(
                    false,
                    $"Invoice cannot exceed {CorporateInvoice.MaxLineItems} line items. Narrow the billing period and try again.",
                    null);
            }

            bookingInputs = corporateBookings
                .Where(cb => cb.Booking != null)
                .Select(cb =>
                {
                    var booking = cb.Booking;
                    var memberName = cb.Membership?.User?.FullName;
                    return new InvoiceBookingChargeInput(
                        booking.Id,
                        cb.AllocationId,
                        booking.TotalAmount,
                        booking.StartDateTime,
                        booking.EndDateTime,
                        booking.Status,
                        cb.IsVisitorBooking,
                        memberName,
                        cb.VisitorName,
                        booking.ParkingSpace?.Title);
                })
                .ToList();
        }

        List<CorporateInvoiceLineDraft> lines;
        try
        {
            lines = _calculator.BuildLines(
                company.BillingType,
                periodStart,
                periodEnd,
                allocationInputs,
                bookingInputs).ToList();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }

        try
        {
            var invoice = CorporateInvoice.Create(
                command.CompanyId,
                company.BillingType,
                periodStart,
                periodEnd,
                command.AdminUserId,
                lines);

            await _uow.Invoices.AddAsync(invoice, ct);
            await _uow.SaveChangesAsync(ct);

            return new ApiResponse<CorporateInvoiceDetailDto>(
                true,
                "Draft invoice generated.",
                CorporateMapping.ToInvoiceDetailDto(invoice));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException or ArgumentOutOfRangeException)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }
    }
}

public class IssueCorporateInvoiceHandler
    : ICommandHandler<IssueCorporateInvoiceCommand, ApiResponse<CorporateInvoiceDetailDto>>
{
    private readonly ICorporateUnitOfWork _uow;

    public IssueCorporateInvoiceHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateInvoiceDetailDto>> HandleAsync(
        IssueCorporateInvoiceCommand command,
        CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Only company admins can issue invoices.", null);
        }

        var invoice = await _uow.Invoices.GetByIdWithLinesAsync(command.CompanyId, command.InvoiceId, ct);
        if (invoice == null)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Invoice not found.", null);
        }

        try
        {
            invoice.Issue(command.AdminUserId);
            await _uow.SaveChangesAsync(ct);
            return new ApiResponse<CorporateInvoiceDetailDto>(true, "Invoice issued.", CorporateMapping.ToInvoiceDetailDto(invoice));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }
    }
}

public class MarkCorporateInvoicePaidHandler
    : ICommandHandler<MarkCorporateInvoicePaidCommand, ApiResponse<CorporateInvoiceDetailDto>>
{
    private readonly ICorporateUnitOfWork _uow;

    public MarkCorporateInvoicePaidHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateInvoiceDetailDto>> HandleAsync(
        MarkCorporateInvoicePaidCommand command,
        CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Only company admins can mark invoices paid.", null);
        }

        var invoice = await _uow.Invoices.GetByIdWithLinesAsync(command.CompanyId, command.InvoiceId, ct);
        if (invoice == null)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Invoice not found.", null);
        }

        try
        {
            invoice.MarkPaid(command.AdminUserId, command.Dto.PaymentReference, command.Dto.PaymentNotes);
            await _uow.SaveChangesAsync(ct);
            return new ApiResponse<CorporateInvoiceDetailDto>(true, "Invoice marked as paid.", CorporateMapping.ToInvoiceDetailDto(invoice));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }
    }
}

public class VoidCorporateInvoiceHandler
    : ICommandHandler<VoidCorporateInvoiceCommand, ApiResponse<CorporateInvoiceDetailDto>>
{
    private readonly ICorporateUnitOfWork _uow;

    public VoidCorporateInvoiceHandler(ICorporateUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<ApiResponse<CorporateInvoiceDetailDto>> HandleAsync(
        VoidCorporateInvoiceCommand command,
        CancellationToken ct = default)
    {
        var membership = await _uow.Companies.GetMembershipAsync(command.CompanyId, command.AdminUserId, ct);
        if (membership == null || !membership.IsAdmin)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Only company admins can void invoices.", null);
        }

        var invoice = await _uow.Invoices.GetByIdWithLinesAsync(command.CompanyId, command.InvoiceId, ct);
        if (invoice == null)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, "Invoice not found.", null);
        }

        try
        {
            invoice.Void(command.AdminUserId, command.Dto.Reason);
            await _uow.SaveChangesAsync(ct);
            return new ApiResponse<CorporateInvoiceDetailDto>(true, "Invoice voided.", CorporateMapping.ToInvoiceDetailDto(invoice));
        }
        catch (Exception ex) when (ex is DomainException or InvalidOperationException or ArgumentException)
        {
            return new ApiResponse<CorporateInvoiceDetailDto>(false, ex.Message, null);
        }
    }
}
