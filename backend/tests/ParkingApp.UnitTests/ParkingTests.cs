using ParkingApp.Application.Interfaces;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Marketplace.Application.Commands.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Messaging.Application.DTOs;
using ParkingApp.Notifications.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Identity.Application.Interfaces;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Corporate.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Identity.Contracts;

namespace ParkingApp.UnitTests;

public class ParkingTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
    private readonly Mock<IUserLookup> _mockUsers;
    private readonly Mock<ICacheService> _mockCache;
    
    private readonly Mock<ILogger<CreateParkingHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<UpdateParkingHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteParkingHandler>> _mockDeleteLogger;
    private readonly Mock<ILogger<ToggleActiveParkingHandler>> _mockToggleLogger;

    public ParkingTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockParkingRepository = new Mock<IParkingSpaceRepository>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockBookingRepository = new Mock<IBookingRepository>();
        _mockUsers = new Mock<IUserLookup>();
        _mockCache = new Mock<ICacheService>();
        
        _mockCreateLogger = new Mock<ILogger<CreateParkingHandler>>();
        _mockUpdateLogger = new Mock<ILogger<UpdateParkingHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteParkingHandler>>();
        _mockToggleLogger = new Mock<ILogger<ToggleActiveParkingHandler>>();

        _mockUnitOfWork.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepository.Object);
        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);
        _mockUnitOfWork.Setup(u => u.Bookings).Returns(_mockBookingRepository.Object);
    }



    [Fact]
    public async Task CreateParkingHandler_WhenOwnerIsVendor_ShouldSucceed()
    {
        // Arrange
        var handler = new CreateParkingHandler(_mockUnitOfWork.Object, _mockUsers.Object, _mockCreateLogger.Object);
        var ownerId = Guid.NewGuid();
        _mockUsers.Setup(r => r.GetByIdAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserSummary(ownerId, "test@example.com", "Test", "User", "1", IsActive: true));

        var dto = new CreateParkingSpaceDto(
            "Premium Park", "Desc", "123 Street", "Tech City", "TS", "IN", "560001", 
            12.97, 77.59, ParkingType.Covered, 5, 40, 300, 2000, 7000, null, null);

        // Act
        var result = await handler.HandleAsync(new CreateParkingCommand(ownerId, dto));

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Title.Should().Be("Premium Park");
        
        _mockParkingRepository.Verify(r => r.AddAsync(It.IsAny<ParkingSpace>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateParkingHandler_WhenUnauthorized_ShouldReturnFailure()
    {
        // Arrange
        var handler = new UpdateParkingHandler(_mockUnitOfWork.Object, _mockUpdateLogger.Object);
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var dto = new UpdateParkingSpaceDto(
            "New Title", null, null, null, null, null, null, null, null, null, null, 
            null, null, null, null, null, null, null, null, null, null, null, null);

        // Act
        var result = await handler.HandleAsync(new UpdateParkingCommand(parkingId, otherUserId, dto));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Unauthorized");
    }

    [Fact]
    public async Task DeleteParkingHandler_WhenSuccessful_ShouldRemoveAndRaiseDomainEvent()
    {
        // Arrange
        var handler = new DeleteParkingHandler(_mockUnitOfWork.Object, _mockDeleteLogger.Object);
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepository.Setup(r => r.HasBlockingBookingsForSpaceAsync(parkingId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // No bookings

        // Act
        var result = await handler.HandleAsync(new DeleteParkingCommand(parkingId, ownerId));

        // Assert
        result.Success.Should().BeTrue();
        _mockParkingRepository.Verify(r => r.Update(parking), Times.Once);
        // Cache invalidation is handled by ParkingSpaceDeletedCacheHandler after SaveChanges
        parking.IsDeleted.Should().BeTrue();
        parking.IsActive.Should().BeFalse();
        parking.DomainEvents.Should().ContainSingle(e => e is ParkingApp.Marketplace.Domain.Events.ParkingSpaceDeletedEvent);
    }

    [Fact]
    public async Task ToggleActiveHandler_WhenParkingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ToggleActiveParkingHandler(_mockUnitOfWork.Object, _mockToggleLogger.Object);
        _mockParkingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        // Act
        var result = await handler.HandleAsync(new ToggleActiveParkingCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }
}






