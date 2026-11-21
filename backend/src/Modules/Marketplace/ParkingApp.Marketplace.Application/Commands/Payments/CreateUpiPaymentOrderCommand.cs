using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Commands.Payments;

public sealed record CreateUpiPaymentOrderCommand(
    Guid UserId,
    Guid BookingId,
    bool PayOverstayFee,
    string? ClientIp,
    string? Referer,
    string? UserAgent,
    bool IsDevelopment
) : ICommand<ApiResponse<UpiCheckoutDto>>;

internal sealed class CreateUpiPaymentOrderHandler
    : ICommandHandler<CreateUpiPaymentOrderCommand, ApiResponse<UpiCheckoutDto>>
{
    private readonly IMarketplaceUnitOfWork _unitOfWork;
    private readonly IPaymentCheckoutStore _checkoutStore;
    private readonly IUpiCheckoutService _upiCheckout;
    private readonly IUserLookup _userLookup;
    private readonly ICacheService _cache;
    private readonly IOptions<UpiPaymentsOptions> _upiOptions;
    private readonly TimeProvider _timeProvider;
    private readonly IUnpaidExpiryService? _unpaidExpiry;
    private readonly ILogger<CreateUpiPaymentOrderHandler> _logger;

    public CreateUpiPaymentOrderHandler(
        IMarketplaceUnitOfWork unitOfWork,
        IPaymentCheckoutStore checkoutStore,
        IUpiCheckoutService upiCheckout,
        IUserLookup userLookup,
        ICacheService cache,
        IOptions<UpiPaymentsOptions> upiOptions,
        TimeProvider timeProvider,
        ILogger<CreateUpiPaymentOrderHandler> logger,
        IUnpaidExpiryService? unpaidExpiry = null)
    {
        _unitOfWork = unitOfWork;
        _checkoutStore = checkoutStore;
        _upiCheckout = upiCheckout;
        _userLookup = userLookup;
        _cache = cache;
        _upiOptions = upiOptions;
        _timeProvider = timeProvider;
        _unpaidExpiry = unpaidExpiry;
        _logger = logger;
    }

    public async Task<ApiResponse<UpiCheckoutDto>> HandleAsync(
        CreateUpiPaymentOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!_upiOptions.Value.Enabled)
            return Fail(PaymentErrorCodes.UpiDisabled, "UPI payments are not enabled");

        var booking = await _unitOfWork.Bookings.GetByIdAsync(command.BookingId, cancellationToken);
        if (booking == null)
            return new ApiResponse<UpiCheckoutDto>(false, "Booking not found", null);
        if (booking.UserId != command.UserId)
            return new ApiResponse<UpiCheckoutDto>(false, "Unauthorized", null);

        if (_unpaidExpiry is not null)
            await _unpaidExpiry.ExpireIfDueAsync(booking, cancellationToken);

        if (!PaymentChargeResolver.TryResolve(booking, command.PayOverstayFee, out var charge, out var error))
            return new ApiResponse<UpiCheckoutDto>(false, error, null);

        var lockKey = PaymentLock.Key(booking.Id, charge.Purpose);
        if (!await _cache.AcquireLockAsync(lockKey, PaymentLock.Ttl, cancellationToken))
            return Fail(PaymentErrorCodes.PaymentInProgress, "A payment is already in progress");

        try
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var liveOrders = await _checkoutStore.ListLiveOrdersAsync(booking.Id, charge.Purpose, cancellationToken);
            var liveUpi = liveOrders.FirstOrDefault(o =>
                string.Equals(o.Rail, PaymentOrder.RailUpi, StringComparison.Ordinal));

            if (liveUpi is not null
                && liveUpi.ExpiresAtUtc is DateTime expires
                && expires - now >= PaymentLock.ReplayMinRemaining)
            {
                return new ApiResponse<UpiCheckoutDto>(true, null, ToDto(liveUpi));
            }

            foreach (var live in liveOrders)
            {
                live.Cancel();
                _checkoutStore.Update(live);
            }

            var user = await _userLookup.GetByIdAsync(command.UserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(user?.PhoneNumber))
                return Fail(PaymentErrorCodes.UpiContactRequired, "A phone number is required for UPI");
            if (!IndianMobileNormalizer.TryNormalize(user.PhoneNumber, out var contact))
                return Fail(PaymentErrorCodes.UpiContactInvalid, "Phone number is not a valid Indian mobile");

            if (!TryResolvePublicClientIp(command.ClientIp, command.IsDevelopment, out var clientIp))
                return Fail(PaymentErrorCodes.UpiIpRequired, "Client IP is required for UPI");

            var referer = NormalizeReferer(command.Referer);
            var userAgent = NormalizeUserAgent(command.UserAgent);
            var receipt = $"{charge.Purpose}:{booking.Id:N}";
            if (receipt.Length > 40)
                receipt = receipt[..40];

            var session = await _upiCheckout.CreateQrSessionAsync(
                new UpiQrSessionRequest(
                    charge.Amount,
                    "INR",
                    receipt,
                    user.Email,
                    contact,
                    clientIp,
                    referer,
                    userAgent,
                    charge.Notes),
                cancellationToken);

            var ttlSeconds = Math.Clamp(_upiOptions.Value.QrTtlSeconds, 60, 3600);
            var expiresAt = now.AddSeconds(ttlSeconds);
            var order = PaymentOrder.Create(
                booking.Id,
                charge.Purpose,
                PaymentOrder.RailUpi,
                PaymentOrder.ProviderRazorpay,
                session.ProviderOrderId,
                charge.Amount,
                "INR",
                PaymentOrderStatus.Pending,
                session.ProviderPaymentId,
                expiresAt,
                JsonSerializer.Serialize(new UpiOrderMetadata(session.UpiIntentUrl, session.DisplayVpa)));
            _checkoutStore.Add(order);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ApiResponse<UpiCheckoutDto>(true, null, ToDto(order));
        }
        catch (Exception ex) when (UniqueConstraint.IsViolation(ex))
        {
            _unitOfWork.ClearChangeTracker();
            return Fail(PaymentErrorCodes.PaymentInProgress, "A payment is already in progress");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to create UPI payment order for booking {BookingId}", command.BookingId);
            return new ApiResponse<UpiCheckoutDto>(false, "Failed to create UPI payment order", null);
        }
        finally
        {
            await _cache.ReleaseLockAsync(lockKey, cancellationToken);
        }
    }

    internal static bool TryResolvePublicClientIp(string? clientIp, bool isDevelopment, out string ip)
    {
        ip = string.Empty;
        if (string.IsNullOrWhiteSpace(clientIp))
            return false;
        if (!IPAddress.TryParse(clientIp.Trim(), out var parsed))
            return false;
        if (parsed.IsIPv4MappedToIPv6)
            parsed = parsed.MapToIPv4();
        if (IPAddress.IsLoopback(parsed))
        {
            if (!isDevelopment)
                return false;
            ip = "127.0.0.1";
            return true;
        }

        ip = parsed.ToString();
        return true;
    }

    internal static string NormalizeReferer(string? referer)
    {
        if (!string.IsNullOrWhiteSpace(referer)
            && referer.Length <= 256
            && (referer.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || referer.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return referer;
        }

        return "https://pe.fyro.cloud/";
    }

    internal static string NormalizeUserAgent(string? userAgent)
    {
        if (!string.IsNullOrWhiteSpace(userAgent))
            return userAgent.Trim();
        return "ParkEase/web";
    }

    private static ApiResponse<UpiCheckoutDto> Fail(string code, string message) =>
        new(false, message, null, Code: code);

    internal static UpiCheckoutDto ToDto(PaymentOrder order)
    {
        var meta = string.IsNullOrWhiteSpace(order.MetadataJson)
            ? null
            : JsonSerializer.Deserialize<UpiOrderMetadata>(order.MetadataJson);
        return new UpiCheckoutDto(
            order.ProviderOrderId,
            order.ProviderPaymentId ?? string.Empty,
            meta?.UpiIntentUrl ?? string.Empty,
            order.Amount,
            order.Currency,
            order.ExpiresAtUtc ?? DateTime.UtcNow,
            meta?.DisplayVpa);
    }
}

internal sealed record UpiOrderMetadata(string UpiIntentUrl, string? DisplayVpa);
