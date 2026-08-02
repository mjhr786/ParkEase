using FluentAssertions;
using Moq;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Application.Commands.Reviews;
using ParkingApp.Marketplace.Application.Queries.Reviews;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.IntegrationTests.Reviews;

/// <summary>
/// API P2 — marketplace reviews must not expose or mutate corporate-only inventory.
/// </summary>
public class ReviewCorporateIsolationIntegrationTests
{
    [Fact]
    public async Task Create_OnCorporateOnlyParking_IsRejected()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedCorporateParking();
        var userId = Guid.NewGuid();

        var result = await fx.CreateReviewHandler().HandleAsync(
            new CreateReviewCommand(userId, new CreateReviewDto(parking.Id, null, 5, "HQ", "Nice")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
        fx.AllReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_OnPublicParking_Succeeds()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedPublicParking();
        var userId = Guid.NewGuid();

        var result = await fx.CreateReviewHandler().HandleAsync(
            new CreateReviewCommand(userId, new CreateReviewDto(parking.Id, null, 4, "Good", "Clean")));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ParkingSpaceId.Should().Be(parking.Id);
        result.Data.Rating.Should().Be(4);
        parking.TotalReviews.Should().Be(1);
        fx.AllReviews.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WithCorporateStagedBooking_IsRejected()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedPublicParking();
        var userId = Guid.NewGuid();
        var staged = fx.SeedCorporateStagedCompletedBooking(userId, parking.Id);

        var result = await fx.CreateReviewHandler().HandleAsync(
            new CreateReviewCommand(
                userId,
                new CreateReviewDto(parking.Id, staged.Id, 5, "Corp stay", "N/A")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid booking reference");
        fx.AllReviews.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_WithValidCompletedMarketplaceBooking_Succeeds()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedPublicParking();
        var userId = Guid.NewGuid();
        var booking = fx.SeedCompletedMarketplaceBooking(userId, parking.Id);

        var result = await fx.CreateReviewHandler().HandleAsync(
            new CreateReviewCommand(
                userId,
                new CreateReviewDto(parking.Id, booking.Id, 5, "Great stay", "Would return")));

        result.Success.Should().BeTrue();
        result.Data!.BookingId.Should().Be(booking.Id);
        fx.AllReviews.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListByParking_WhenCorporateOnly_ReturnsEmptyAndSkipsReadStore()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedCorporateParking();
        fx.SeedReview(Guid.NewGuid(), parking.Id);

        var result = await fx.GetReviewsByParkingSpaceHandler()
            .HandleAsync(new GetReviewsByParkingSpaceQuery(parking.Id));

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull().And.BeEmpty();
        fx.ReviewReadStore.Verify(
            r => r.GetByParkingSpaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ListByParking_WhenPublic_ReturnsReviews()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedPublicParking();
        fx.SeedReview(Guid.NewGuid(), parking.Id, rating: 5);
        fx.SeedReview(Guid.NewGuid(), parking.Id, rating: 3);

        var result = await fx.GetReviewsByParkingSpaceHandler()
            .HandleAsync(new GetReviewsByParkingSpaceQuery(parking.Id));

        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_WhenCorporateOnly_ReturnsNotFound()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedCorporateParking();
        var review = fx.SeedReview(Guid.NewGuid(), parking.Id);

        var result = await fx.GetReviewByIdHandler()
            .HandleAsync(new GetReviewByIdQuery(review.Id));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Review not found");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetById_WhenPublic_ReturnsReview()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedPublicParking();
        var review = fx.SeedReview(Guid.NewGuid(), parking.Id);

        var result = await fx.GetReviewByIdHandler()
            .HandleAsync(new GetReviewByIdQuery(review.Id));

        result.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(review.Id);
        result.Data.Title.Should().Be("Solid");
    }

    [Fact]
    public async Task Update_WhenCorporateOnly_ReturnsNotFound()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedCorporateParking();
        var userId = Guid.NewGuid();
        var review = fx.SeedReview(userId, parking.Id);

        var result = await fx.UpdateReviewHandler().HandleAsync(
            new UpdateReviewCommand(review.Id, userId, new UpdateReviewDto(1, "x", "y")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Review not found");
        review.Rating.Should().Be(5);
    }

    [Fact]
    public async Task Delete_OwnReviewOnCorporateOnly_IsAllowedForOrphanCleanup()
    {
        var fx = new InMemoryMarketplaceFixture();
        var parking = fx.SeedCorporateParking();
        var userId = Guid.NewGuid();
        var review = fx.SeedReview(userId, parking.Id);
        parking.TotalReviews = 1;
        parking.AverageRating = 5;

        var result = await fx.DeleteReviewHandler()
            .HandleAsync(new DeleteReviewCommand(review.Id, userId));

        result.Success.Should().BeTrue();
        fx.AllReviews.Should().BeEmpty();
        parking.TotalReviews.Should().Be(0);
    }

    [Fact]
    public async Task OwnerResponse_WhenCorporateOnly_IsUnauthorized()
    {
        var fx = new InMemoryMarketplaceFixture();
        var ownerId = Guid.NewGuid();
        var parking = fx.SeedCorporateParking(ownerId: ownerId);
        var review = fx.SeedReview(Guid.NewGuid(), parking.Id);

        var result = await fx.AddOwnerResponseHandler().HandleAsync(
            new AddOwnerResponseCommand(review.Id, ownerId, new OwnerResponseDto("Thanks")));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
        review.OwnerResponse.Should().BeNull();
    }

    [Fact]
    public async Task OwnerResponse_WhenPublicAndOwner_Succeeds()
    {
        var fx = new InMemoryMarketplaceFixture();
        var ownerId = Guid.NewGuid();
        var parking = fx.SeedPublicParking(ownerId: ownerId);
        var review = fx.SeedReview(Guid.NewGuid(), parking.Id);

        var result = await fx.AddOwnerResponseHandler().HandleAsync(
            new AddOwnerResponseCommand(review.Id, ownerId, new OwnerResponseDto("Appreciate it")));

        result.Success.Should().BeTrue();
        review.OwnerResponse.Should().Be("Appreciate it");
        review.OwnerResponseAt.Should().NotBeNull();
    }
}
