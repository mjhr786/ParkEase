using ParkingApp.Identity.Infrastructure.ModuleAdapters;
using ParkingApp.Identity.Domain.Interfaces;
using FluentAssertions;
using Moq;
using ParkingApp.Identity.Contracts;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Infrastructure.ModuleAdapters;
using Xunit;

namespace ParkingApp.UnitTests.Contracts;

public class ModuleLookupAdapterTests
{
    [Fact]
    public async Task UserLookup_Maps_User_To_UserSummary()
    {
        var userId = Guid.NewGuid();
        var user = User.Register("ada@example.com", "hash", "Ada", "Lovelace", "555");
        user.Id = userId;

        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        IUserLookup lookup = new UserLookup(repo.Object);
        var summary = await lookup.GetByIdAsync(userId);

        summary.Should().NotBeNull();
        summary!.UserId.Should().Be(userId);
        summary.Email.Should().Be("ada@example.com");
        summary.FirstName.Should().Be("Ada");
        summary.LastName.Should().Be("Lovelace");
        summary.FullName.Should().Be("Ada Lovelace");
        summary.IsActive.Should().BeTrue();
        summary.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task UserLookup_Returns_Null_When_Missing()
    {
        var repo = new Mock<IUserRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        IUserLookup lookup = new UserLookup(repo.Object);
        var summary = await lookup.GetByIdAsync(Guid.NewGuid());

        summary.Should().BeNull();
    }

    [Fact]
    public async Task ParkingSpaceLookup_Maps_To_Summary()
    {
        var spaceId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var space = ParkingSpace.CreateForVendor(
            ownerId,
            "Downtown Lot",
            "Desc",
            "1 Main",
            "City",
            "State",
            "Country",
            "00000",
            12.0,
            77.0,
            ParkingType.Open,
            10,
            50,
            200,
            1000,
            3000);

        // CreateForVendor sets Id; force known id for assertion
        space.Id = spaceId;

        var repo = new Mock<IParkingSpaceRepository>();
        repo.Setup(r => r.GetByIdAsync(spaceId, It.IsAny<CancellationToken>())).ReturnsAsync(space);

        IParkingSpaceLookup lookup = new ParkingSpaceLookup(repo.Object);
        var summary = await lookup.GetByIdAsync(spaceId);

        summary.Should().NotBeNull();
        summary!.ParkingSpaceId.Should().Be(spaceId);
        summary.OwnerId.Should().Be(ownerId);
        summary.Title.Should().Be("Downtown Lot");
        summary.IsActive.Should().BeTrue();
        summary.TotalSpots.Should().Be(10);
        summary.OwnershipType.Should().Be("IndividualVendor");
        summary.IsCompanyOwned.Should().BeFalse();
    }

    [Fact]
    public async Task BookingLookup_Maps_To_Snapshot_With_String_Status()
    {
        var bookingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var spaceId = Guid.NewGuid();
        var start = DateTime.UtcNow.AddHours(1);
        var end = start.AddHours(2);

        var booking = Booking.CreateMarketplace(
            userId,
            spaceId,
            start,
            end,
            PricingType.Hourly,
            VehicleType.Car,
            baseAmount: 100,
            taxAmount: 0,
            serviceFee: 0,
            discountAmount: 0,
            totalAmount: 100);

        booking.Id = bookingId;

        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.GetByIdAsync(bookingId, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        IBookingLookup lookup = new BookingLookup(repo.Object);
        var snapshot = await lookup.GetByIdAsync(bookingId);

        snapshot.Should().NotBeNull();
        snapshot!.BookingId.Should().Be(bookingId);
        snapshot.UserId.Should().Be(userId);
        snapshot.ParkingSpaceId.Should().Be(spaceId);
        snapshot.StartUtc.Should().Be(start);
        snapshot.EndUtc.Should().Be(end);
        snapshot.Status.Should().Be(nameof(BookingStatus.Pending));
    }
}





