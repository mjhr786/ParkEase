using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.Caching;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Commands.EventPackages;
using ParkingApp.Marketplace.Application.Commands.Favorites;
using ParkingApp.Marketplace.Application.Commands.FileUpload;
using ParkingApp.Marketplace.Application.Commands.Reviews;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Queries.Bookings;
using ParkingApp.Marketplace.Application.Queries.EventPackages;
using ParkingApp.Marketplace.Application.Queries.Favorites;
using ParkingApp.Marketplace.Application.Queries.FileUpload;
using ParkingApp.Marketplace.Application.Queries.Parking;
using ParkingApp.Marketplace.Application.Queries.Reviews;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Infrastructure.Persistence;
using Xunit;

namespace ParkingApp.UnitTests.Marketplace;

/// <summary>
/// KD-9 / KD-9a: public marketplace product APIs must hide IsCorporateOnly inventory
/// (independent of channel isolation flag).
/// </summary>
public class CorporateOnlyInventoryIsolationTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IParkingSpaceRepository> _parkingRepo = new();
    private readonly Mock<IBookingRepository> _bookingRepo = new();
    private readonly Mock<IFavoriteRepository> _favoriteRepo = new();
    private readonly Mock<IEventParkingPackageRepository> _eventPackageRepo = new();
    private readonly Mock<IReviewRepository> _reviewRepo = new();
    private readonly Mock<IReviewReadStore> _reviewReadStore = new();
    private readonly Mock<ICacheService> _cache = new();

    public CorporateOnlyInventoryIsolationTests()
    {
        _uow.Setup(u => u.ParkingSpaces).Returns(_parkingRepo.Object);
        _uow.Setup(u => u.Bookings).Returns(_bookingRepo.Object);
        _uow.Setup(u => u.Favorites).Returns(_favoriteRepo.Object);
        _uow.Setup(u => u.EventParkingPackages).Returns(_eventPackageRepo.Object);
        _uow.Setup(u => u.Reviews).Returns(_reviewRepo.Object);
    }

    private static ParkingSpaceDto PublicDto(Guid id) =>
        new(
            id,
            Guid.NewGuid(),
            "Owner",
            "Title",
            "Desc",
            "Addr",
            "City",
            "ST",
            "IN",
            "123",
            12.0,
            77.0,
            ParkingType.Open,
            10,
            10,
            50,
            400,
            2000,
            7000,
            TimeSpan.FromHours(8),
            TimeSpan.FromHours(20),
            true,
            new List<string>(),
            new List<VehicleType>(),
            new List<string>(),
            true,
            true,
            4.5,
            10,
            null,
            DateTime.UtcNow,
            IsCorporateOnly: false);

    private static ParkingSpaceDto CorporateDto(Guid id) =>
        PublicDto(id) with { IsCorporateOnly = true };

    // ── GetParkingById (KD-9a cache order) ──────────────────────────────────

    [Fact]
    public async Task GetParkingById_WhenCorporateOnlyInWarmPublicCache_StillReturnsNotFound()
    {
        var logger = new Mock<ILogger<GetParkingByIdHandler>>();
        var handler = new GetParkingByIdHandler(_uow.Object, _cache.Object, logger.Object);
        var parkingId = Guid.NewGuid();
        var cacheKey = CacheKeys.Parking(parkingId);

        _cache
            .Setup(c => c.GetAsync<ParkingSpaceDto>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CorporateDto(parkingId));

        var result = await handler.HandleAsync(new GetParkingByIdQuery(parkingId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        result.Data.Should().BeNull();
        // Must not fall through to DB after rejecting poisoned cache, and must not re-write cache.
        _parkingRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<ParkingSpaceDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
        // Heal the poisoned public key so the next miss revalidates against the DB.
        _cache.Verify(c => c.RemoveAsync(cacheKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetParkingById_WhenEntityIsCorporateOnly_ReturnsNotFoundWithoutCaching()
    {
        var logger = new Mock<ILogger<GetParkingByIdHandler>>();
        var handler = new GetParkingByIdHandler(_uow.Object, _cache.Object, logger.Object);
        var parkingId = Guid.NewGuid();
        var cacheKey = CacheKeys.Parking(parkingId);

        _cache
            .Setup(c => c.GetAsync<ParkingSpaceDto>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceDto?)null);
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true, Title = "HQ Lot" });

        var result = await handler.HandleAsync(new GetParkingByIdQuery(parkingId));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        result.Data.Should().BeNull();
        _cache.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<ParkingSpaceDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _bookingRepo.Verify(
            r => r.GetActiveBookingsForSpacesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetParkingById_WhenPublicSpace_CachesAndReturns()
    {
        var logger = new Mock<ILogger<GetParkingByIdHandler>>();
        var handler = new GetParkingByIdHandler(_uow.Object, _cache.Object, logger.Object);
        var parkingId = Guid.NewGuid();
        var cacheKey = CacheKeys.Parking(parkingId);

        _cache
            .Setup(c => c.GetAsync<ParkingSpaceDto>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpaceDto?)null);
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace
            {
                Id = parkingId,
                IsCorporateOnly = false,
                Title = "Public Lot",
                TotalSpots = 5,
                AvailableSpots = 5
            });
        _bookingRepo
            .Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var result = await handler.HandleAsync(new GetParkingByIdQuery(parkingId));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.IsCorporateOnly.Should().BeFalse();
        _cache.Verify(
            c => c.SetAsync(cacheKey, It.IsAny<ParkingSpaceDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── GetOwnerParkings ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOwnerParkings_ExcludesCorporateOnlyFromDbList()
    {
        var handler = new GetOwnerParkingsHandler(_uow.Object, _cache.Object);
        var ownerId = Guid.NewGuid();
        var publicId = Guid.NewGuid();
        var corporateId = Guid.NewGuid();

        _cache
            .Setup(c => c.GetAsync<List<ParkingSpaceDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<ParkingSpaceDto>?)null);
        _parkingRepo
            .Setup(r => r.GetByOwnerIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpace>
            {
                new() { Id = publicId, OwnerId = ownerId, IsCorporateOnly = false, Title = "Driveway" },
                new() { Id = corporateId, OwnerId = ownerId, IsCorporateOnly = true, Title = "Company HQ" }
            });
        _bookingRepo
            .Setup(r => r.GetActiveBookingsForSpacesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Booking>());

        var result = await handler.HandleAsync(new GetOwnerParkingsQuery(ownerId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Id.Should().Be(publicId);
        result.Data[0].IsCorporateOnly.Should().BeFalse();
    }

    [Fact]
    public async Task GetOwnerParkings_WhenWarmCacheHasCorporateOnly_StripsThem()
    {
        var handler = new GetOwnerParkingsHandler(_uow.Object, _cache.Object);
        var ownerId = Guid.NewGuid();
        var publicId = Guid.NewGuid();
        var corporateId = Guid.NewGuid();
        var cacheKey = CacheKeys.OwnerParkings(ownerId);

        _cache
            .Setup(c => c.GetAsync<List<ParkingSpaceDto>>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ParkingSpaceDto>
            {
                PublicDto(publicId),
                CorporateDto(corporateId)
            });

        var result = await handler.HandleAsync(new GetOwnerParkingsQuery(ownerId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data![0].Id.Should().Be(publicId);
        _parkingRepo.Verify(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── CreateBooking ───────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBooking_WhenCorporateOnly_ReturnsNotAvailable()
    {
        var handler = new CreateBookingHandler(_uow.Object, _cache.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true, Title = "Corp" });

        var command = new CreateBookingCommand(
            Guid.NewGuid(),
            parkingId,
            DateTime.UtcNow.AddHours(1),
            DateTime.UtcNow.AddHours(3),
            PricingType.Hourly,
            VehicleType.Car,
            null,
            "KA01AB1234",
            null,
            null,
            null);

        var result = await handler.HandleAsync(command);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space is not available");
        result.Data.Should().BeNull();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── CalculatePrice ──────────────────────────────────────────────────────

    [Fact]
    public async Task CalculatePrice_WhenCorporateOnly_ReturnsNotFound()
    {
        var handler = new CalculatePriceHandler(_uow.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true, HourlyRate = 100 });

        var query = new CalculatePriceQuery(
            parkingId,
            DateTime.UtcNow,
            DateTime.UtcNow.AddHours(2),
            (int)PricingType.Hourly,
            null);

        var result = await handler.HandleAsync(query);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        result.Data.Should().BeNull();
    }

    // ── Favorites ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleFavorite_WhenCorporateOnlyAndNoExistingFavorite_ReturnsNotFound()
    {
        var handler = new ToggleFavoriteCommandHandler(_uow.Object);
        var parkingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true });
        _favoriteRepo
            .Setup(r => r.GetByUserAndSpaceAsync(userId, parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);

        var result = await handler.HandleAsync(new ToggleFavoriteCommand(userId, parkingId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
        result.Data.Should().BeFalse();
        _favoriteRepo.Verify(
            r => r.AddAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ToggleFavorite_WhenCorporateOnlyWithExistingFavorite_AllowsRemove()
    {
        var handler = new ToggleFavoriteCommandHandler(_uow.Object);
        var parkingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existing = new Favorite { UserId = userId, ParkingSpaceId = parkingId, IsDeleted = false };

        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true });
        _favoriteRepo
            .Setup(r => r.GetByUserAndSpaceAsync(userId, parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await handler.HandleAsync(new ToggleFavoriteCommand(userId, parkingId));

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Removed from favorites");
        result.Data.Should().BeFalse();
        _favoriteRepo.Verify(r => r.Remove(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyFavorites_ExcludesCorporateOnlySpaces()
    {
        var handler = new GetMyFavoritesQueryHandler(_uow.Object);
        var userId = Guid.NewGuid();
        var publicSpace = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            Title = "Public",
            IsCorporateOnly = false,
            AllowedVehicleTypes = "Car",
            Amenities = "CCTV"
        };
        var corporateSpace = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            Title = "Corporate",
            IsCorporateOnly = true,
            AllowedVehicleTypes = "Car",
            Amenities = "CCTV"
        };

        _favoriteRepo
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Favorite>
            {
                new() { UserId = userId, ParkingSpaceId = publicSpace.Id, ParkingSpace = publicSpace },
                new() { UserId = userId, ParkingSpaceId = corporateSpace.Id, ParkingSpace = corporateSpace }
            });

        var result = await handler.HandleAsync(new GetMyFavoritesQuery(userId));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(1);
        result.Data!.Single().Title.Should().Be("Public");
        result.Data!.Single().IsCorporateOnly.Should().BeFalse();
    }

    // ── Residual public-by-id product surfaces ───────────────────────────────

    [Fact]
    public async Task GetParkingFiles_WhenCorporateOnly_ReturnsEmptyList()
    {
        var handler = new GetParkingFilesHandler(_uow.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace
            {
                Id = parkingId,
                IsCorporateOnly = true,
                ImageUrls = "https://cdn.example/corp.jpg"
            });

        var result = await handler.HandleAsync(new GetParkingFilesQuery(parkingId));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetEventPackagesForParking_WhenCorporateOnly_ReturnsNotFound()
    {
        var handler = new GetEventPackagesForParkingHandler(_uow.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true, Title = "HQ" });

        var result = await handler.HandleAsync(new GetEventPackagesForParkingQuery(parkingId, ActiveOnly: true));

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not found");
        result.Data.Should().BeNull();
        _eventPackageRepo.Verify(
            r => r.GetByParkingSpaceIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Reviews (KD-9 residual) ─────────────────────────────────────────────

    [Fact]
    public async Task CreateReview_WhenCorporateOnly_ReturnsNotFound()
    {
        var logger = new Mock<ILogger<CreateReviewHandler>>();
        var handler = new CreateReviewHandler(_uow.Object, _cache.Object, logger.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true, Title = "HQ Lot" });

        var result = await handler.HandleAsync(new CreateReviewCommand(
            Guid.NewGuid(),
            new CreateReviewDto(parkingId, null, 5, "Great", "Nice")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
        result.Data.Should().BeNull();
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateReview_WhenCorporateStagedBooking_ReturnsInvalidBookingReference()
    {
        var logger = new Mock<ILogger<CreateReviewHandler>>();
        var handler = new CreateReviewHandler(_uow.Object, _cache.Object, logger.Object);
        var userId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = false });
        _bookingRepo
            .Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Booking
            {
                Id = bookingId,
                UserId = userId,
                ParkingSpaceId = parkingId,
                Status = BookingStatus.Completed,
                IsCorporateStaged = true
            });

        var result = await handler.HandleAsync(new CreateReviewCommand(
            userId,
            new CreateReviewDto(parkingId, bookingId, 5, "Ok", "Comment")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid booking reference");
        _reviewRepo.Verify(r => r.AddAsync(It.IsAny<Review>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReviewsByParkingSpace_WhenCorporateOnly_ReturnsEmptyWithoutReadStoreOrCache()
    {
        var handler = new GetReviewsByParkingSpaceHandler(_uow.Object, _reviewReadStore.Object, _cache.Object);
        var parkingId = Guid.NewGuid();
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true });

        var result = await handler.HandleAsync(new GetReviewsByParkingSpaceQuery(parkingId));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
        _reviewReadStore.Verify(
            r => r.GetByParkingSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _cache.Verify(
            c => c.GetAsync<List<ReviewDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _cache.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<List<ReviewDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetReviewById_WhenParkingIsCorporateOnly_ReturnsNotFound()
    {
        var handler = new GetReviewByIdHandler(_uow.Object);
        var reviewId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        _reviewRepo
            .Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Review { Id = reviewId, ParkingSpaceId = parkingId, Title = "Secret" });
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true });

        var result = await handler.HandleAsync(new GetReviewByIdQuery(reviewId));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Review not found");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReview_WhenParkingIsCorporateOnly_ReturnsNotFound()
    {
        var handler = new UpdateReviewHandler(_uow.Object, _cache.Object);
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        _reviewRepo
            .Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Review { Id = reviewId, UserId = userId, ParkingSpaceId = parkingId, Rating = 4 });
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, IsCorporateOnly = true });

        var result = await handler.HandleAsync(
            new UpdateReviewCommand(reviewId, userId, new UpdateReviewDto(5, "Nope", "Nope")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Review not found");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddOwnerResponse_WhenParkingIsCorporateOnly_ReturnsUnauthorized()
    {
        var handler = new AddOwnerResponseHandler(_uow.Object, _cache.Object);
        var ownerId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var parkingId = Guid.NewGuid();
        _reviewRepo
            .Setup(r => r.GetByIdAsync(reviewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Review { Id = reviewId, ParkingSpaceId = parkingId });
        _parkingRepo
            .Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ParkingSpace { Id = parkingId, OwnerId = ownerId, IsCorporateOnly = true });

        var result = await handler.HandleAsync(
            new AddOwnerResponseCommand(reviewId, ownerId, new OwnerResponseDto("Thanks")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
