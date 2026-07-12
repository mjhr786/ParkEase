using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Mappings;
using ParkingApp.Application.Services;
using ParkingApp.BuildingBlocks.Extensions;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.Application.CQRS.Queries.Bookings;

public class GetBookingByIdHandler : IQueryHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetBookingByIdHandler(IMarketplaceUnitOfWork unitOfWork)
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
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public GetBookingByReferenceHandler(IMarketplaceUnitOfWork unitOfWork)
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
    private readonly IBookingReadStore _readStore;

    public GetUserBookingsHandler(IBookingReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetUserBookingsQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _readStore.GetUserBookingsAsync(query.UserId, query.Filter, cancellationToken);
        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}

public class GetVendorBookingsHandler : IQueryHandler<GetVendorBookingsQuery, ApiResponse<BookingListResultDto>>
{
    private readonly IBookingReadStore _readStore;

    public GetVendorBookingsHandler(IBookingReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<ApiResponse<BookingListResultDto>> HandleAsync(GetVendorBookingsQuery query, CancellationToken cancellationToken = default)
    {
        var result = await _readStore.GetVendorBookingsAsync(query.VendorId, query.Filter, cancellationToken);
        return new ApiResponse<BookingListResultDto>(true, null, result);
    }
}

public class CalculatePriceHandler : IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IParkingPassPricingService _pricingService;

    public CalculatePriceHandler(IMarketplaceUnitOfWork unitOfWork, IParkingPassPricingService pricingService)
    {
        _unitOfWork = unitOfWork;
        _pricingService = pricingService;
    }

    public CalculatePriceHandler(IMarketplaceUnitOfWork unitOfWork)
        : this(unitOfWork, new ParkingPassPricingService(unitOfWork))
    {
    }

    public async Task<ApiResponse<PriceBreakdownDto>> HandleAsync(CalculatePriceQuery query, CancellationToken cancellationToken = default)
    {
        var parking = await _unitOfWork.ParkingSpaces.GetByIdAsync(query.ParkingSpaceId, cancellationToken);
        if (parking == null)
        {
            return new ApiResponse<PriceBreakdownDto>(false, "Parking space not found", null);
        }

        var pricingResult = await _pricingService.CalculateAsync(
            query.UserId,
            parking,
            query.StartDateTime.ToUtc(),
            query.EndDateTime.ToUtc(),
            (PricingType)query.PricingType,
            query.DiscountCode,
            null,
            cancellationToken);

        var breakdown = new PriceBreakdownDto(
            pricingResult.BaseAmount,
            pricingResult.TaxAmount,
            pricingResult.ServiceFee,
            pricingResult.DiscountAmount,
            pricingResult.TotalAmount,
            pricingResult.PricingDescription,
            pricingResult.Duration,
            pricingResult.DurationUnit,
            pricingResult.ParkingPassId,
            pricingResult.ParkingPassType,
            pricingResult.AppliedDiscountPercentage,
            pricingResult.IsPassApplied);

        return new ApiResponse<PriceBreakdownDto>(true, null, breakdown);
    }
}

public class GetPendingRequestsCountHandler : IQueryHandler<GetPendingRequestsCountQuery, ApiResponse<int>>
{
    private readonly IBookingReadStore _readStore;

    public GetPendingRequestsCountHandler(IBookingReadStore readStore)
    {
        _readStore = readStore;
    }

    public async Task<ApiResponse<int>> HandleAsync(GetPendingRequestsCountQuery query, CancellationToken cancellationToken = default)
    {
        var pendingCount = await _readStore.CountPendingForVendorAsync(query.VendorId, cancellationToken);
        return new ApiResponse<int>(true, null, pendingCount);
    }
}
