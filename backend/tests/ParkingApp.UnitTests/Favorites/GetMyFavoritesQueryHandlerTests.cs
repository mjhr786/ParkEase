using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Queries.Favorites;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.Favorites;

public class GetMyFavoritesQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IFavoriteRepository> _mockFavoriteRepo;

    public GetMyFavoritesQueryHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockFavoriteRepo = new Mock<IFavoriteRepository>();
        _mockUnitOfWork.Setup(u => u.Favorites).Returns(_mockFavoriteRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFavoriteParkingSpaces()
    {
        // Arrange
        var handler = new GetMyFavoritesQueryHandler(_mockUnitOfWork.Object);
        var userId = Guid.NewGuid();
        var query = new GetMyFavoritesQuery(userId);

        var parkingSpace1 = new ParkingSpace 
        { 
            Id = Guid.NewGuid(), 
            Title = "Space 1", 
            Owner = new User { FirstName = "Owner 1" },
            AllowedVehicleTypes = "Car,Bike",
            Amenities = "CCTV,Lighting"
        };
        var parkingSpace2 = new ParkingSpace 
        { 
            Id = Guid.NewGuid(), 
            Title = "Space 2", 
            Owner = new User { FirstName = "Owner 2" },
            AllowedVehicleTypes = "Car",
            Amenities = "Security"
        };

        var favorites = new List<Favorite>
        {
            new Favorite { UserId = userId, ParkingSpaceId = parkingSpace1.Id, ParkingSpace = parkingSpace1 },
            new Favorite { UserId = userId, ParkingSpaceId = parkingSpace2.Id, ParkingSpace = parkingSpace2 }
        };

        _mockFavoriteRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(favorites);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data!.Any(f => f.Title == "Space 1").Should().BeTrue();
        result.Data!.Any(f => f.Title == "Space 2").Should().BeTrue();
    }
}
