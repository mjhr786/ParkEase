using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class UnpaidExpiryRulesTests
{
    [Fact]
    public void InstantBookWithNoUpiOrder_IsNotMember()
    {
        var booking = NewAwaiting();
        UnpaidExpiryService.IsExpiryMember(booking, Array.Empty<PaymentOrder>(), DateTime.UtcNow, 30)
            .Should().BeFalse();
    }

    [Fact]
    public void LiveUpiOrder_IsNotMember()
    {
        var booking = NewAwaiting();
        var order = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_1", 10m, "INR", PaymentOrderStatus.Pending, "pay_1",
            DateTime.UtcNow.AddMinutes(-20));

        UnpaidExpiryService.IsExpiryMember(booking, new[] { order }, DateTime.UtcNow, 30)
            .Should().BeFalse();
    }

    [Fact]
    public void ExpiredUpiOrder_AfterGrace_IsMember()
    {
        var booking = NewAwaiting();
        var expiredAt = DateTime.UtcNow.AddMinutes(-40);
        var order = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_1", 10m, "INR", PaymentOrderStatus.Expired, "pay_1",
            expiredAt);

        UnpaidExpiryService.IsExpiryMember(booking, new[] { order }, DateTime.UtcNow, 30)
            .Should().BeTrue();
    }

    [Fact]
    public void StripeOnlyOrder_IsNotMember()
    {
        var booking = NewAwaiting();
        var order = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailStripe, PaymentOrder.ProviderStripe,
            "pi_1", 10m, "INR", PaymentOrderStatus.Captured, "pi_1",
            DateTime.UtcNow.AddMinutes(-40));

        UnpaidExpiryService.IsExpiryMember(booking, new[] { order }, DateTime.UtcNow, 30)
            .Should().BeFalse();
    }

    [Fact]
    public void ExpiredUpi_WithLiveStripeCreated_IsNotMember()
    {
        var booking = NewAwaiting();
        var now = DateTime.UtcNow;
        var expiredUpi = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_1", 10m, "INR", PaymentOrderStatus.Expired, "pay_1",
            now.AddMinutes(-40));
        var liveStripe = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailStripe, PaymentOrder.ProviderStripe,
            "pi_live", 10m, "INR", PaymentOrderStatus.Created, "pi_live",
            now.AddMinutes(10));

        UnpaidExpiryService.IsExpiryMember(booking, new[] { expiredUpi, liveStripe }, now, 30)
            .Should().BeFalse();
    }

    [Fact]
    public void ExpiredUpi_BeforeGrace_IsNotMember()
    {
        var booking = NewAwaiting();
        var now = DateTime.UtcNow;
        var order = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_1", 10m, "INR", PaymentOrderStatus.Expired, "pay_1",
            now.AddMinutes(-10));

        UnpaidExpiryService.IsExpiryMember(booking, new[] { order }, now, 30)
            .Should().BeFalse();
    }

    [Fact]
    public void ExtensionBooking_IsNotMember()
    {
        var booking = NewAwaiting();
        booking.Status = BookingStatus.AwaitingExtensionPayment;
        var order = PaymentOrder.Create(
            booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_1", 10m, "INR", PaymentOrderStatus.Expired, "pay_1",
            DateTime.UtcNow.AddMinutes(-40));

        UnpaidExpiryService.IsExpiryMember(booking, new[] { order }, DateTime.UtcNow, 30)
            .Should().BeFalse();
    }

    private static Booking NewAwaiting()
    {
        var booking = new Booking
        {
            UserId = Guid.NewGuid(),
            ParkingSpaceId = Guid.NewGuid(),
            Status = BookingStatus.AwaitingPayment,
            TotalAmount = 50m
        };
        return booking;
    }
}
