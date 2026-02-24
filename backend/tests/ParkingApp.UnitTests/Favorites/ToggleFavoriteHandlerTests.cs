using Moq;
using FluentAssertions;
using Xunit;
using ParkingApp.Application.CQRS.Commands.Favorites;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;

namespace ParkingApp.UnitTests.Favorites;

public class ToggleFavoriteHandlerTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IFavoriteRepository> _mockFavoriteRepo;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;

    public ToggleFavoriteHandlerTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockFavoriteRepo = new Mock<IFavoriteRepository>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();

        _mockUnitOfWork.Setup(u => u.Favorites).Returns(_mockFavoriteRepo.Object);
        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenParkingSpaceNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ToggleFavoriteCommandHandler(_mockUnitOfWork.Object);
        var parkingSpaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new ToggleFavoriteCommand(userId, parkingSpaceId);

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ParkingSpace?)null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
        result.Data.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyFavorited_ShouldRemoveFavorite()
    {
        // Arrange
        var handler = new ToggleFavoriteCommandHandler(_mockUnitOfWork.Object);
        var parkingSpaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new ToggleFavoriteCommand(userId, parkingSpaceId);

        var parkingSpace = new ParkingSpace { Id = parkingSpaceId };
        var existingFavorite = new Favorite { UserId = userId, ParkingSpaceId = parkingSpaceId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);
        _mockFavoriteRepo.Setup(r => r.GetByUserAndSpaceAsync(userId, parkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingFavorite);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Removed from favorites");
        result.Data.Should().BeFalse();
        _mockFavoriteRepo.Verify(r => r.Remove(existingFavorite), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenNotFavorited_ShouldAddFavorite()
    {
        // Arrange
        var handler = new ToggleFavoriteCommandHandler(_mockUnitOfWork.Object);
        var parkingSpaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new ToggleFavoriteCommand(userId, parkingSpaceId);

        var parkingSpace = new ParkingSpace { Id = parkingSpaceId };

        _mockParkingRepo.Setup(r => r.GetByIdAsync(parkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parkingSpace);
        _mockFavoriteRepo.Setup(r => r.GetByUserAndSpaceAsync(userId, parkingSpaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Favorite?)null);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Added to favorites");
        result.Data.Should().BeTrue();
        _mockFavoriteRepo.Verify(r => r.AddAsync(It.IsAny<Favorite>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
