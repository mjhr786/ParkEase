using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Events.Parking;

namespace ParkingApp.UnitTests;

public class ParkingTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepository;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IBookingRepository> _mockBookingRepository;
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
    public async Task CreateParkingHandler_WithNonVendorRole_ShouldReturnFailure()
    {
        // Arrange
        var handler = new CreateParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, Role = UserRole.Member };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var dto = new CreateParkingSpaceDto(
            "Test Parking", "Description", "Address", "City", "State", "Country", "12345", 
            12.34, 56.78, ParkingType.Open, 10, 50, 400, 2500, 10000, null, null);

        // Act
        var result = await handler.HandleAsync(new CreateParkingCommand(ownerId, dto));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Only vendors can create parking spaces");
    }

    [Fact]
    public async Task CreateParkingHandler_WhenOwnerIsVendor_ShouldSucceed()
    {
        // Arrange
        var handler = new CreateParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockCreateLogger.Object);
        var ownerId = Guid.NewGuid();
        var owner = new User { Id = ownerId, Role = UserRole.Vendor, Email = "vendor@test.com", FirstName = "Vendor" };
        
        _mockUserRepository.Setup(r => r.GetByIdAsync(ownerId, It.IsAny<CancellationToken>())).ReturnsAsync(owner);

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
        var handler = new UpdateParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockUpdateLogger.Object);
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
    public async Task DeleteParkingHandler_WhenSuccessful_ShouldRemoveAndInvalidateCache()
    {
        // Arrange
        var handler = new DeleteParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockDeleteLogger.Object);
        var parkingId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = parkingId, OwnerId = ownerId };
        
        _mockParkingRepository.Setup(r => r.GetByIdAsync(parkingId, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepository.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Booking, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // No bookings

        // Act
        var result = await handler.HandleAsync(new DeleteParkingCommand(parkingId, ownerId));

        // Assert
        result.Success.Should().BeTrue();
        _mockParkingRepository.Verify(r => r.Remove(parking), Times.Once);
        _mockCache.Verify(c => c.RemoveByPatternAsync("search:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ToggleActiveHandler_WhenParkingNotFound_ShouldReturnFailure()
    {
        // Arrange
        var handler = new ToggleActiveParkingHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockToggleLogger.Object);
        _mockParkingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace?)null);

        // Act
        var result = await handler.HandleAsync(new ToggleActiveParkingCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Parking space not found");
    }
}
