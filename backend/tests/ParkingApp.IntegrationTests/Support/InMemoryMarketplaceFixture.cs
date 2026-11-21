using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ParkingApp.Application.Interfaces;
using ParkingApp.Marketplace.Application.Commands.Payments;
using ParkingApp.Marketplace.Application.Commands.Reviews;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Queries.Reviews;
using ParkingApp.Marketplace.Application.Services;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// Lightweight in-memory marketplace surface for application-layer integration tests.
/// Tracks bookings, payments, parking, and reviews without a database.
/// </summary>
internal sealed class InMemoryMarketplaceFixture
{
    private readonly List<ParkingSpace> _parkings = new();
    private readonly List<Booking> _bookings = new();
    private readonly List<Payment> _payments = new();
    private readonly List<Review> _reviews = new();

    public Mock<IMarketplaceUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IParkingSpaceRepository> ParkingSpaces { get; } = new();
    public Mock<IBookingRepository> Bookings { get; } = new();
    public Mock<IPaymentRepository> Payments { get; } = new();
    public Mock<IReviewRepository> Reviews { get; } = new();
    public Mock<ICacheService> Cache { get; } = new();
    public Mock<IReviewReadStore> ReviewReadStore { get; } = new();
    public Mock<IPaymentCheckoutStore> CheckoutStore { get; } = new();
    public DeterministicPaymentService PaymentService { get; } = new();
    public FakeUpiCheckoutService UpiCheckout { get; } = new();
    public List<PaymentOrder> PaymentOrders { get; } = new();

    public InMemoryMarketplaceFixture()
    {
        UnitOfWork.Setup(u => u.ParkingSpaces).Returns(ParkingSpaces.Object);
        UnitOfWork.Setup(u => u.Bookings).Returns(Bookings.Object);
        UnitOfWork.Setup(u => u.Payments).Returns(Payments.Object);
        UnitOfWork.Setup(u => u.Reviews).Returns(Reviews.Object);
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        ParkingSpaces
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _parkings.FirstOrDefault(p => p.Id == id));

        Bookings
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _bookings.FirstOrDefault(b => b.Id == id));
        Bookings
            .Setup(r => r.GetByParkingSpaceIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid spaceId, CancellationToken _) =>
                _bookings.Where(b => b.ParkingSpaceId == spaceId).ToList().AsEnumerable());
        Bookings
            .Setup(r => r.Update(It.IsAny<Booking>()))
            .Callback<Booking>(b => { /* tracked by reference */ });

        Payments
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _payments.FirstOrDefault(p => p.Id == id));
        Payments
            .Setup(r => r.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bookingId, CancellationToken _) =>
                _payments.FirstOrDefault(p => p.BookingId == bookingId));
        Payments
            .Setup(r => r.AddAsync(It.IsAny<Payment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment p, CancellationToken _) =>
            {
                if (p.Id == Guid.Empty)
                    p.Id = Guid.NewGuid();
                _payments.Add(p);
                return p;
            });
        Payments
            .Setup(r => r.Update(It.IsAny<Payment>()))
            .Callback<Payment>(_ => { /* tracked by reference */ });

        Reviews
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _reviews.FirstOrDefault(r => r.Id == id));
        Reviews
            .Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Review, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<Review, bool>> pred, CancellationToken _) =>
                _reviews.AsQueryable().FirstOrDefault(pred));
        Reviews
            .Setup(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Review review, CancellationToken _) =>
            {
                if (review.Id == Guid.Empty)
                    review.Id = Guid.NewGuid();
                _reviews.Add(review);
                return review;
            });
        Reviews
            .Setup(r => r.Update(It.IsAny<Review>()))
            .Callback<Review>(_ => { });
        Reviews
            .Setup(r => r.Remove(It.IsAny<Review>()))
            .Callback<Review>(r => _reviews.Remove(r));

        ReviewReadStore
            .Setup(r => r.GetByParkingSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid spaceId, CancellationToken _) =>
                _reviews
                    .Where(r => r.ParkingSpaceId == spaceId)
                    .Select(r => new ParkingApp.Marketplace.Contracts.DTOs.ReviewDto(
                        r.Id,
                        r.UserId,
                        "User",
                        r.ParkingSpaceId,
                        r.BookingId,
                        r.Rating,
                        r.Title,
                        r.Comment,
                        r.HelpfulCount,
                        r.OwnerResponse,
                        r.OwnerResponseAt,
                        r.CreatedAt))
                    .ToList());

        Cache
            .Setup(c => c.GetAsync<List<ParkingApp.Marketplace.Contracts.DTOs.ReviewDto>>(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ParkingApp.Marketplace.Contracts.DTOs.ReviewDto>?)null);
        Cache
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<List<ParkingApp.Marketplace.Contracts.DTOs.ReviewDto>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Cache
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Cache
            .Setup(c => c.AcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Cache
            .Setup(c => c.ReleaseLockAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        CheckoutStore
            .Setup(s => s.FindLiveOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bookingId, string purpose, CancellationToken _) =>
                PaymentOrders.FirstOrDefault(o =>
                    o.BookingId == bookingId
                    && o.Purpose == purpose
                    && o.IsLive));
        CheckoutStore
            .Setup(s => s.ListLiveOrdersAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bookingId, string purpose, CancellationToken _) =>
                (IReadOnlyList<PaymentOrder>)PaymentOrders
                    .Where(o => o.BookingId == bookingId && o.Purpose == purpose && o.IsLive)
                    .ToList());
        CheckoutStore
            .Setup(s => s.FindCapturedOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bookingId, string purpose, CancellationToken _) =>
                PaymentOrders.FirstOrDefault(o =>
                    o.BookingId == bookingId
                    && o.Purpose == purpose
                    && o.Status == ParkingApp.Marketplace.Contracts.Enums.PaymentOrderStatus.Captured));
        CheckoutStore
            .Setup(s => s.FindByProviderPaymentIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string provider, string providerPaymentId, CancellationToken _) =>
                PaymentOrders.FirstOrDefault(o =>
                    o.Provider == provider && o.ProviderPaymentId == providerPaymentId));
        CheckoutStore
            .Setup(s => s.FindByProviderOrderIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string provider, string providerOrderId, CancellationToken _) =>
                PaymentOrders.FirstOrDefault(o =>
                    o.Provider == provider && o.ProviderOrderId == providerOrderId));
        CheckoutStore
            .Setup(s => s.FindByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => PaymentOrders.FirstOrDefault(o => o.Id == id));
        CheckoutStore
            .Setup(s => s.LockOrderAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bookingId, string purpose, CancellationToken _) =>
                PaymentOrders
                    .Where(o => o.BookingId == bookingId && o.Purpose == purpose)
                    .OrderByDescending(o => o.IsLive)
                    .ThenByDescending(o => o.CreatedAt)
                    .FirstOrDefault());
        CheckoutStore
            .Setup(s => s.Add(It.IsAny<PaymentOrder>()))
            .Callback<PaymentOrder>(o => PaymentOrders.Add(o));
        CheckoutStore
            .Setup(s => s.Update(It.IsAny<PaymentOrder>()));
        CheckoutStore
            .Setup(s => s.ReloadAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    public ParkingSpace SeedPublicParking(Guid? id = null, Guid? ownerId = null)
    {
        var space = new ParkingSpace
        {
            Id = id ?? Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Title = "Public Driveway",
            IsCorporateOnly = false,
            TotalReviews = 0,
            AverageRating = 0,
            TotalSpots = 2,
            AvailableSpots = 2,
            IsBayGuidanceEnabled = false
        };
        _parkings.Add(space);
        return space;
    }

    public ParkingSpace SeedCorporateParking(Guid? id = null, Guid? ownerId = null)
    {
        var space = new ParkingSpace
        {
            Id = id ?? Guid.NewGuid(),
            OwnerId = ownerId ?? Guid.NewGuid(),
            Title = "Company HQ",
            IsCorporateOnly = true,
            TotalReviews = 0,
            AverageRating = 0,
            TotalSpots = 10,
            AvailableSpots = 10,
            IsBayGuidanceEnabled = false
        };
        _parkings.Add(space);
        return space;
    }

    public Booking SeedAwaitingPaymentBooking(Guid userId, Guid parkingSpaceId, decimal amount = 150m)
    {
        var booking = Booking.CreateMarketplace(
            userId,
            parkingSpaceId,
            DateTime.UtcNow.AddHours(2),
            DateTime.UtcNow.AddHours(4),
            ParkingApp.Marketplace.Contracts.Enums.PricingType.Hourly,
            ParkingApp.BuildingBlocks.Enums.VehicleType.Car,
            baseAmount: amount,
            taxAmount: 0,
            serviceFee: 0,
            discountAmount: 0,
            totalAmount: amount);
        booking.AwaitPayment();
        _bookings.Add(booking);
        return booking;
    }

    public Booking SeedCompletedMarketplaceBooking(Guid userId, Guid parkingSpaceId)
    {
        var booking = Booking.CreateMarketplace(
            userId,
            parkingSpaceId,
            DateTime.UtcNow.AddHours(-4),
            DateTime.UtcNow.AddHours(-2),
            ParkingApp.Marketplace.Contracts.Enums.PricingType.Hourly,
            ParkingApp.BuildingBlocks.Enums.VehicleType.Car,
            baseAmount: 100,
            taxAmount: 0,
            serviceFee: 0,
            discountAmount: 0,
            totalAmount: 100);
        booking.AwaitPayment();
        booking.Confirm();
        booking.Status = ParkingApp.Marketplace.Contracts.Enums.BookingStatus.Completed;
        _bookings.Add(booking);
        return booking;
    }

    public Booking SeedCorporateStagedCompletedBooking(Guid userId, Guid parkingSpaceId)
    {
        var booking = Booking.CreateCorporateEmployee(
            userId,
            parkingSpaceId,
            DateTime.UtcNow.AddHours(-3),
            DateTime.UtcNow.AddHours(-1),
            ParkingApp.BuildingBlocks.Enums.VehicleType.Car,
            totalAmount: 0,
            vehicleNumber: "KA01AB1234");
        booking.Status = ParkingApp.Marketplace.Contracts.Enums.BookingStatus.Completed;
        _bookings.Add(booking);
        return booking;
    }

    public Booking SeedBooking(Booking booking)
    {
        _bookings.Add(booking);
        return booking;
    }

    public Review SeedReview(Guid userId, Guid parkingSpaceId, int rating = 5)
    {
        var review = new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ParkingSpaceId = parkingSpaceId,
            Rating = rating,
            Title = "Solid",
            Comment = "Worked well",
            CreatedAt = DateTime.UtcNow
        };
        _reviews.Add(review);
        return review;
    }

    public CreatePaymentOrderHandler CreateOrderHandler() =>
        new(UnitOfWork.Object, PaymentService, CheckoutStore.Object, Cache.Object, NullLogger<CreatePaymentOrderHandler>.Instance);

    public VerifyPaymentHandler VerifyHandler()
    {
        var orchestrator = new PaymentCaptureOrchestrator(
            UnitOfWork.Object,
            CheckoutStore.Object,
            NullLogger<PaymentCaptureOrchestrator>.Instance);
        return new VerifyPaymentHandler(
            UnitOfWork.Object,
            PaymentService,
            Cache.Object,
            CheckoutStore.Object,
            UpiCheckout,
            orchestrator,
            NullLogger<VerifyPaymentHandler>.Instance);
    }

    public ProcessRefundHandler RefundHandler() =>
        new(UnitOfWork.Object, PaymentService, NullLogger<ProcessRefundHandler>.Instance);

    public CreateReviewHandler CreateReviewHandler() =>
        new(UnitOfWork.Object, Cache.Object, NullLogger<CreateReviewHandler>.Instance);

    public GetReviewByIdHandler GetReviewByIdHandler() =>
        new(UnitOfWork.Object);

    public GetReviewsByParkingSpaceHandler GetReviewsByParkingSpaceHandler() =>
        new(UnitOfWork.Object, ReviewReadStore.Object, Cache.Object);

    public UpdateReviewHandler UpdateReviewHandler() =>
        new(UnitOfWork.Object, Cache.Object);

    public DeleteReviewHandler DeleteReviewHandler() =>
        new(UnitOfWork.Object, Cache.Object);

    public AddOwnerResponseHandler AddOwnerResponseHandler() =>
        new(UnitOfWork.Object, Cache.Object);

    public IReadOnlyList<Payment> AllPayments => _payments;
    public IReadOnlyList<Review> AllReviews => _reviews;
}
