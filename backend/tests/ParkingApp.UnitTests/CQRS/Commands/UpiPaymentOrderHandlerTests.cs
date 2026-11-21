using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Application.Queries.Payments;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Infrastructure.Persistence;
using ICacheService = ParkingApp.Application.Interfaces.ICacheService;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class UpiPaymentOrderHandlerTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IPaymentCheckoutStore> _store = new();
    private readonly Mock<IUpiCheckoutService> _upi = new();
    private readonly Mock<IUserLookup> _users = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly Mock<ILogger<CreateUpiPaymentOrderHandler>> _logger = new();
    private readonly List<PaymentOrder> _live = new();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Booking _booking;

    public UpiPaymentOrderHandlerTests()
    {
        _booking = new Booking
        {
            UserId = _userId,
            Status = BookingStatus.AwaitingPayment,
            TotalAmount = 150
        };
        _uow.Setup(u => u.Bookings).Returns(_bookings.Object);
        _bookings.Setup(b => b.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_booking);
        _cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _cache.Setup(c => c.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _store.Setup(s => s.ListLiveOrdersAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _live.Where(o => o.IsLive).ToList());
        _store.Setup(s => s.Add(It.IsAny<PaymentOrder>()))
            .Callback<PaymentOrder>(o => _live.Add(o));
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "ada@example.com", "Ada", "Lovelace", PhoneNumber: "+91 98765 43210"));
        _upi.Setup(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpiQrSession("order_abc", "pay_abc", "upi://pay?pa=merchant@upi&pn=ParkEase", "merchant@upi"));
    }

    private CreateUpiPaymentOrderHandler Handler(bool enabled = true) =>
        new(
            _uow.Object,
            _store.Object,
            _upi.Object,
            _users.Object,
            _cache.Object,
            Options.Create(new UpiPaymentsOptions { Enabled = enabled, QrTtlSeconds = 900 }),
            TimeProvider.System,
            _logger.Object);

    private CreateUpiPaymentOrderCommand Cmd(
        string? ip = "203.0.113.10",
        bool isDevelopment = false,
        string? referer = "https://pe.fyro.cloud/checkout",
        string? userAgent = "ParkEase/web") =>
        new(_userId, _booking.Id, false, ip, referer, userAgent, isDevelopment);

    [Fact]
    public async Task FlagOff_ReturnsUpiDisabled_WithoutCallingRazorpay()
    {
        var result = await Handler(enabled: false).HandleAsync(Cmd());

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.UpiDisabled);
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EmptyPhone_ReturnsUpiContactRequired_WithoutCallingRazorpay()
    {
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "ada@example.com", "Ada", "Lovelace", PhoneNumber: "  "));

        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.UpiContactRequired);
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Plus91Phone_NormalizesContactToTenDigits()
    {
        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeTrue();
        result.Data!.UpiIntentUrl.Should().StartWith("upi://pay?");
        _upi.Verify(s => s.CreateQrSessionAsync(
            It.Is<UpiQrSessionRequest>(r => r.Contact == "9876543210"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidPhone_ReturnsUpiContactInvalid()
    {
        _users.Setup(u => u.GetByIdAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(_userId, "ada@example.com", "Ada", "Lovelace", PhoneNumber: "12345"));

        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.UpiContactInvalid);
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProductionLoopbackIp_ReturnsUpiIpRequired()
    {
        var result = await Handler().HandleAsync(Cmd(ip: "127.0.0.1", isDevelopment: false));

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.UpiIpRequired);
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LockNotAcquired_ReturnsPaymentInProgress()
    {
        _cache.Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.PaymentInProgress);
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UniqueConstraintOnSave_ReturnsPaymentInProgress()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("23505 duplicate key value violates unique constraint"));

        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeFalse();
        result.Code.Should().Be(PaymentErrorCodes.PaymentInProgress);
    }

    [Fact]
    public async Task SecondLiveUpiWithEnoughTtl_ReplaysExistingIntent()
    {
        var existing = PaymentOrder.Create(
            _booking.Id,
            "booking",
            PaymentOrder.RailUpi,
            PaymentOrder.ProviderRazorpay,
            "order_live",
            150,
            "INR",
            PaymentOrderStatus.Pending,
            "pay_live",
            DateTime.UtcNow.AddMinutes(10),
            """{"UpiIntentUrl":"upi://pay?pa=merchant@upi&pn=ParkEase","DisplayVpa":"merchant@upi"}""");
        _live.Add(existing);

        var result = await Handler().HandleAsync(Cmd());

        result.Success.Should().BeTrue();
        result.Data!.ProviderOrderId.Should().Be("order_live");
        result.Data.UpiIntentUrl.Should().Be("upi://pay?pa=merchant@upi&pn=ParkEase");
        _upi.Verify(s => s.CreateQrSessionAsync(It.IsAny<UpiQrSessionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void UpiCreateOrderDto_HasNoPhoneNumberProperty()
    {
        typeof(UpiCreateOrderDto).GetProperty("PhoneNumber").Should().BeNull();
        typeof(UpiCreateOrderDto).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo("BookingId", "PayOverstayFee");
    }

    [Fact]
    public async Task GetUpiOrder_NonOwner_ReturnsNotFound()
    {
        var order = PaymentOrder.Create(
            _booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_secret", 150, "INR", PaymentOrderStatus.Pending, "pay_secret",
            DateTime.UtcNow.AddMinutes(10),
            """{"UpiIntentUrl":"upi://pay?pa=m@upi","DisplayVpa":"m@upi"}""");
        _store.Setup(s => s.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderRazorpay, "order_secret", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new GetUpiPaymentOrderHandler(_uow.Object, _store.Object);
        var result = await handler.HandleAsync(new GetUpiPaymentOrderQuery(Guid.NewGuid(), "order_secret"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Order not found");
    }

    [Fact]
    public async Task GetUpiOrder_Owner_ReturnsDtoWithoutConfirming()
    {
        var order = PaymentOrder.Create(
            _booking.Id, "booking", PaymentOrder.RailUpi, PaymentOrder.ProviderRazorpay,
            "order_mine", 150, "INR", PaymentOrderStatus.Pending, "pay_mine",
            DateTime.UtcNow.AddMinutes(10),
            """{"UpiIntentUrl":"upi://pay?pa=m@upi","DisplayVpa":"m@upi"}""");
        _store.Setup(s => s.FindByProviderOrderIdAsync(
                PaymentOrder.ProviderRazorpay, "order_mine", It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);

        var handler = new GetUpiPaymentOrderHandler(_uow.Object, _store.Object);
        var result = await handler.HandleAsync(new GetUpiPaymentOrderQuery(_userId, "order_mine"));

        result.Success.Should().BeTrue();
        result.Data!.UpiIntentUrl.Should().Be("upi://pay?pa=m@upi");
        result.Data.ProviderOrderId.Should().Be("order_mine");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        order.Status.Should().Be(PaymentOrderStatus.Pending);
    }
}
