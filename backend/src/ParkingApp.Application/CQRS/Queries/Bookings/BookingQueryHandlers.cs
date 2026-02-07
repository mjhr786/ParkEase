using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Bookings;

public class GetBookingByIdHandler : IQueryHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBookingByIdHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(GetBookingByIdQuery query, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByIdWithDetailsAsync(query.BookingId, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        // Verify user has access
        if (booking.UserId != query.UserId && booking.ParkingSpace.OwnerId != query.UserId)
        {
            return new ApiResponse<BookingDto>(false, "Unauthorized", null);
        }

        return new ApiResponse<BookingDto>(true, null, booking.ToDto());
    }
}

public class GetBookingByReferenceHandler : IQueryHandler<GetBookingByReferenceQuery, ApiResponse<BookingDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetBookingByReferenceHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingDto>> HandleAsync(GetBookingByReferenceQuery query, CancellationToken cancellationToken = default)
    {
        var booking = await _unitOfWork.Bookings.GetByReferenceAsync(query.Reference, cancellationToken);
        if (booking == null)
        {
            return new ApiResponse<BookingDto>(false, "Booking not found", null);
        }

        return new ApiResponse<BookingDto>(true, null, booking.ToDto());
    }
}

public class GetUserBookingsHandler : IQueryHandler<GetUserBookingsQuery, ApiResponse<BookingListResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserBookingsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetUserBookingsQuery query, CancellationToken cancellationToken = default)
    {
        var bookings = await _unitOfWork.Bookings.GetByUserIdAsync(query.UserId, cancellationToken);
        
        var filtered = bookings.AsQueryable();

        if (query.Filter != null)
        {
            if (query.Filter.Status.HasValue)
                filtered = filtered.Where(b => b.Status == query.Filter.Status.Value);
            if (query.Filter.StartDate.HasValue)
                filtered = filtered.Where(b => b.StartDateTime >= query.Filter.StartDate.Value);
            if (query.Filter.EndDate.HasValue)
                filtered = filtered.Where(b => b.EndDateTime <= query.Filter.EndDate.Value);
        }

        var totalCount = filtered.Count();
        var page = query.Filter?.Page ?? 1;
        var pageSize = query.Filter?.PageSize ?? 10;

        var items = filtered
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.ToDto())
            .ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var result = new BookingListResultDto(items, totalCount, page, pageSize, totalPages);
        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}

public class GetVendorBookingsHandler : IQueryHandler<GetVendorBookingsQuery, ApiResponse<BookingListResultDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetVendorBookingsHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetVendorBookingsQuery query, CancellationToken cancellationToken = default)
    {
        var bookings = await _unitOfWork.Bookings.GetByVendorIdAsync(query.VendorId, cancellationToken);
        
        var filtered = bookings.AsQueryable();

        if (query.Filter != null)
        {
            if (query.Filter.Status.HasValue)
                filtered = filtered.Where(b => b.Status == query.Filter.Status.Value);
            if (query.Filter.StartDate.HasValue)
                filtered = filtered.Where(b => b.StartDateTime >= query.Filter.StartDate.Value);
            if (query.Filter.EndDate.HasValue)
                filtered = filtered.Where(b => b.EndDateTime <= query.Filter.EndDate.Value);
        }

        var totalCount = filtered.Count();
        var page = query.Filter?.Page ?? 1;
        var pageSize = query.Filter?.PageSize ?? 10;

        var items = filtered
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => b.ToDto())
            .ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        var result = new BookingListResultDto(items, totalCount, page, pageSize, totalPages);
        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}

public class CalculatePriceHandler : IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public CalculatePriceHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PriceBreakdownDto>> HandleAsync(CalculatePriceQuery query, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<PriceBreakdownDto>(false, "Parking space not found", null);
        }

        var duration = query.EndDateTime - query.StartDateTime;
        var pricingType = (PricingType)query.PricingType;

        decimal baseAmount = pricingType switch
        {
            PricingType.Hourly => parking.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours),
            PricingType.Daily => parking.DailyRate * (decimal)Math.Ceiling(duration.TotalDays),
            PricingType.Weekly => parking.WeeklyRate * (decimal)Math.Ceiling(duration.TotalDays / 7),
            PricingType.Monthly => parking.MonthlyRate * (decimal)Math.Ceiling(duration.TotalDays / 30),
            _ => parking.HourlyRate * (decimal)Math.Ceiling(duration.TotalHours)
        };

        var taxAmount = baseAmount * 0.18m;
        var serviceFee = baseAmount * 0.05m;
        decimal discountAmount = 0;

        // TODO: Apply discount code logic if needed

        var totalAmount = baseAmount + taxAmount + serviceFee - discountAmount;

        var breakdown = new PriceBreakdownDto(
            baseAmount, taxAmount, serviceFee, discountAmount, totalAmount,
            pricingType.ToString(), (int)duration.TotalHours, "hours"
        );

        return new ApiResponse<PriceBreakdownDto>(true, null, breakdown);
    }
}
