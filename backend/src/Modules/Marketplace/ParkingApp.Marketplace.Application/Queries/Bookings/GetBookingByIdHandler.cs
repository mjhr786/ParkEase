using ParkingApp.Application.CQRS;
using ParkingApp.Application.Caching;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Application.Interfaces;

using ParkingApp.Application.Interfaces;

using ParkingApp.Marketplace.Application.Mappings;


using ParkingApp.Marketplace.Application.Services;
using ParkingApp.BuildingBlocks.Extensions;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Queries.Bookings;

internal sealed class GetBookingByIdHandler : IQueryHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>
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

internal sealed class GetBookingByReferenceHandler : IQueryHandler<GetBookingByReferenceQuery, ApiResponse<BookingDto>>
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

internal sealed class GetUserBookingsHandler : IQueryHandler<GetUserBookingsQuery, ApiResponse<BookingListResultDto>>
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

internal sealed class GetVendorBookingsHandler : IQueryHandler<GetVendorBookingsQuery, ApiResponse<BookingListResultDto>>
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

internal sealed class CalculatePriceHandler : IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>
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
        // KD-9: do not quote marketplace prices for corporate-only inventory.
        if (parking == null || parking.IsCorporateOnly)
        {
            return new ApiResponse<PriceBreakdownDto>(false, "Parking space not found", null);
        }

        var ancillary = await AncillaryServiceResolver.ResolveForBookingAsync(
            _unitOfWork,
            parking.Id,
            query.AncillaryServiceIds,
            requireAllActive: true,
            cancellationToken);
        if (!ancillary.Success)
        {
            return new ApiResponse<PriceBreakdownDto>(
                false,
                ancillary.ErrorMessage ?? "Invalid add-on services",
                null);
        }

        var pricingResult = await _pricingService.CalculateAsync(
            query.UserId,
            parking,
            query.StartDateTime.ToUtc(),
            query.EndDateTime.ToUtc(),
            (PricingType)query.PricingType,
            query.DiscountCode,
            null,
            query.IncludeEvCharging,
            ancillary.Subtotal,
            ancillary.QuoteLines,
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
            pricingResult.IsPassApplied,
            pricingResult.DynamicPricingApplied,
            pricingResult.DynamicMultiplier,
            pricingResult.DynamicPricingFactors,
            pricingResult.IncludeEvCharging,
            pricingResult.EvChargingFeeAmount,
            pricingResult.EvPricingMode,
            pricingResult.EvRatePerKwh,
            pricingResult.AncillarySubtotal,
            pricingResult.AncillaryLines);

        return new ApiResponse<PriceBreakdownDto>(true, null, breakdown);
    }
}

internal sealed class GetPendingRequestsCountHandler : IQueryHandler<GetPendingRequestsCountQuery, ApiResponse<int>>
{
    private readonly IBookingReadStore _readStore;
    private readonly ICacheService _cache;

    public GetPendingRequestsCountHandler(IBookingReadStore readStore, ICacheService cache)
    {
        _readStore = readStore;
        _cache = cache;
    }

    public async Task<ApiResponse<int>> HandleAsync(GetPendingRequestsCountQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.PendingRequestsCount(query.VendorId);
        var cached = await _cache.GetAsync<int?>(cacheKey, cancellationToken);
        if (cached.HasValue)
            return new ApiResponse<int>(true, null, cached.Value);

        var pendingCount = await _readStore.CountPendingForVendorAsync(query.VendorId, cancellationToken);
        await _cache.SetAsync(cacheKey, (int?)pendingCount, TimeSpan.FromMinutes(1), cancellationToken);
        return new ApiResponse<int>(true, null, pendingCount);
    }
}

internal sealed class GetBookingAccessPassHandler : IQueryHandler<GetBookingAccessPassQuery, ApiResponse<BookingAccessPassDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IWalletPassService _walletPassService;

    public GetBookingAccessPassHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IWalletPassService walletPassService)
    {
        _unitOfWork = unitOfWork;
        _walletPassService = walletPassService;
    }

    public async Task<ApiResponse<BookingAccessPassDto>> HandleAsync(
        GetBookingAccessPassQuery query,
        CancellationToken cancellationToken = default)
    {
        var booking = await AccessPassBookingLoader.LoadAuthorizedAsync(
            _unitOfWork, query.BookingId, query.UserId, issueIfMissing: true, cancellationToken);
        if (booking.Error is not null)
            return new ApiResponse<BookingAccessPassDto>(false, booking.Error, null);

        var b = booking.Booking!;
        var token = b.QRCode!;
        var wallet = _walletPassService.GetAvailability();
        var qrDataUrl = _walletPassService.BuildQrDataUrl(token);
        var encoded = Uri.EscapeDataString(token);
        var qrFallback = $"https://api.qrserver.com/v1/create-qr-code/?size=280x280&data={encoded}";

        var dto = new BookingAccessPassDto(
            b.Id,
            b.BookingReference,
            token,
            b.ParkingSpace?.Title ?? "Parking",
            b.ParkingSpace?.Address ?? string.Empty,
            b.StartDateTime,
            b.EndDateTime,
            b.Status,
            b.IsAccessPassValidAt(DateTime.UtcNow),
            b.VehicleNumber,
            QrImageUrl: qrDataUrl ?? qrFallback,
            Payload: token,
            AppleWalletAvailable: wallet.AppleWalletAvailable,
            GoogleWalletAvailable: wallet.GoogleWalletAvailable,
            AppleWalletDownloadPath: wallet.AppleWalletAvailable
                ? $"/api/bookings/{b.Id}/access-pass/apple.pkpass"
                : null,
            GoogleWalletLinkPath: wallet.GoogleWalletAvailable
                ? $"/api/bookings/{b.Id}/access-pass/google-wallet"
                : null,
            WalletStatusMessage: wallet.StatusMessage,
            AppleWalletIsSigned: wallet.AppleIsSigned);

        return new ApiResponse<BookingAccessPassDto>(true, null, dto);
    }
}

internal sealed class GetAppleWalletPassHandler : IQueryHandler<GetAppleWalletPassQuery, ApiResponse<AppleWalletPassFileDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IWalletPassService _walletPassService;

    public GetAppleWalletPassHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IWalletPassService walletPassService)
    {
        _unitOfWork = unitOfWork;
        _walletPassService = walletPassService;
    }

    public async Task<ApiResponse<AppleWalletPassFileDto>> HandleAsync(
        GetAppleWalletPassQuery query,
        CancellationToken cancellationToken = default)
    {
        var booking = await AccessPassBookingLoader.LoadAuthorizedAsync(
            _unitOfWork, query.BookingId, query.UserId, issueIfMissing: true, cancellationToken);
        if (booking.Error is not null)
            return new ApiResponse<AppleWalletPassFileDto>(false, booking.Error, null);

        var b = booking.Booking!;
        var content = AccessPassBookingLoader.ToWalletContent(b);
        var result = _walletPassService.BuildApplePkPass(content);
        if (!result.Success || result.Content is null)
            return new ApiResponse<AppleWalletPassFileDto>(false, result.ErrorMessage ?? "Apple Wallet pass unavailable", null);

        return new ApiResponse<AppleWalletPassFileDto>(
            true,
            null,
            new AppleWalletPassFileDto(
                result.Content,
                result.FileName,
                "application/vnd.apple.pkpass",
                result.IsSigned));
    }
}

internal sealed class GetGoogleWalletSaveLinkHandler : IQueryHandler<GetGoogleWalletSaveLinkQuery, ApiResponse<GoogleWalletSaveLinkDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IWalletPassService _walletPassService;

    public GetGoogleWalletSaveLinkHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IWalletPassService walletPassService)
    {
        _unitOfWork = unitOfWork;
        _walletPassService = walletPassService;
    }

    public async Task<ApiResponse<GoogleWalletSaveLinkDto>> HandleAsync(
        GetGoogleWalletSaveLinkQuery query,
        CancellationToken cancellationToken = default)
    {
        var booking = await AccessPassBookingLoader.LoadAuthorizedAsync(
            _unitOfWork, query.BookingId, query.UserId, issueIfMissing: true, cancellationToken);
        if (booking.Error is not null)
            return new ApiResponse<GoogleWalletSaveLinkDto>(false, booking.Error, null);

        var content = AccessPassBookingLoader.ToWalletContent(booking.Booking!);
        var result = _walletPassService.BuildGoogleSaveLink(content);
        if (!result.Success)
        {
            return new ApiResponse<GoogleWalletSaveLinkDto>(
                false,
                result.ErrorMessage ?? "Google Wallet unavailable",
                new GoogleWalletSaveLinkDto(null, result.IsConfigured, result.ErrorMessage));
        }

        return new ApiResponse<GoogleWalletSaveLinkDto>(
            true,
            null,
            new GoogleWalletSaveLinkDto(result.SaveUrl, true, null));
    }
}

/// <summary>Shared load + auth + optional EnsureAccessPass for wallet / QR queries.</summary>
internal static class AccessPassBookingLoader
{
    internal sealed record LoadResult(Booking? Booking, string? Error);

    public static async Task<LoadResult> LoadAuthorizedAsync(
        IMarketplaceUnitOfWork unitOfWork,
        Guid bookingId,
        Guid userId,
        bool issueIfMissing,
        CancellationToken cancellationToken)
    {
        var booking = await unitOfWork.Bookings.GetByIdWithDetailsAsync(bookingId, cancellationToken);
        if (booking is null)
            return new LoadResult(null, "Booking not found");

        var isGuest = booking.UserId == userId;
        var isOwner = booking.ParkingSpace?.OwnerId == userId;
        if (!isGuest && !isOwner)
            return new LoadResult(null, "Unauthorized");

        if (booking.Status is BookingStatus.Pending or BookingStatus.AwaitingPayment or BookingStatus.Rejected
            or BookingStatus.Cancelled or BookingStatus.Expired or BookingStatus.Completed)
        {
            if (string.IsNullOrWhiteSpace(booking.QRCode))
                return new LoadResult(null, "Access pass is only available for confirmed or active bookings.");
        }
        else if (issueIfMissing && booking.EnsureAccessPass())
        {
            unitOfWork.Bookings.Update(booking);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(booking.QRCode))
            return new LoadResult(null, "Access pass not available");

        return new LoadResult(booking, null);
    }

    public static WalletPassContent ToWalletContent(Booking booking) =>
        new(
            booking.Id,
            booking.BookingReference,
            booking.QRCode!,
            booking.ParkingSpace?.Title ?? "Parking",
            booking.ParkingSpace?.Address ?? string.Empty,
            booking.StartDateTime,
            booking.EndDateTime,
            booking.VehicleNumber);
}

internal sealed class VerifyAccessPassHandler : IQueryHandler<VerifyAccessPassQuery, ApiResponse<AccessPassVerifyResultDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;

    public VerifyAccessPassHandler(IMarketplaceUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<AccessPassVerifyResultDto>> HandleAsync(
        VerifyAccessPassQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
        {
            return new ApiResponse<AccessPassVerifyResultDto>(
                true,
                "Access denied",
                Deny("InvalidToken", "Access token is required."));
        }

        var booking = await _unitOfWork.Bookings.GetByAccessPassTokenAsync(query.Token, cancellationToken);
        if (booking is null)
        {
            return new ApiResponse<AccessPassVerifyResultDto>(
                true,
                "Access denied",
                Deny("UnknownToken", "No booking found for this access pass."));
        }

        // Optional: when requester is a vendor, ensure they own the facility.
        if (query.RequesterUserId is Guid requester
            && requester != Guid.Empty
            && booking.ParkingSpace is not null
            && booking.ParkingSpace.OwnerId != requester
            && booking.UserId != requester)
        {
            return new ApiResponse<AccessPassVerifyResultDto>(
                false,
                "Unauthorized",
                Deny("NotFacilityOwner", "You can only verify passes for your own facilities."));
        }

        var now = DateTime.UtcNow;
        if (!booking.IsAccessPassValidAt(now))
        {
            var reason = booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected
                ? "InvalidState"
                : now < booking.StartDateTime.AddHours(-1)
                    ? "OutsideWindow"
                    : now >= booking.EndDateTime
                        ? "Expired"
                        : "InvalidState";

            return new ApiResponse<AccessPassVerifyResultDto>(
                true,
                "Access denied",
                new AccessPassVerifyResultDto(
                    false,
                    "Denied",
                    reason,
                    reason switch
                    {
                        "OutsideWindow" => "Too early for this booking access window.",
                        "Expired" => "This booking has ended.",
                        _ => "Booking is not valid for access right now."
                    },
                    booking.Id,
                    booking.BookingReference,
                    booking.ParkingSpaceId,
                    booking.ParkingSpace?.Title,
                    booking.Status,
                    booking.StartDateTime,
                    booking.EndDateTime,
                    booking.VehicleNumber));
        }

        return new ApiResponse<AccessPassVerifyResultDto>(
            true,
            "Access granted",
            new AccessPassVerifyResultDto(
                true,
                "Granted",
                null,
                null,
                booking.Id,
                booking.BookingReference,
                booking.ParkingSpaceId,
                booking.ParkingSpace?.Title,
                booking.Status,
                booking.StartDateTime,
                booking.EndDateTime,
                booking.VehicleNumber));
    }

    private static AccessPassVerifyResultDto Deny(string code, string message) =>
        new(false, "Denied", code, message, null, null, null, null, null, null, null, null);
}

