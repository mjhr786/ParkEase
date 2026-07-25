using ParkingApp.Notifications.Application.Queries.Notifications;
using ParkingApp.Notifications.Application.Commands.Notifications;
using ParkingApp.Notifications.Application.EventHandlers;
using ParkingApp.Marketplace.Application.Commands.Bookings;
using ParkingApp.Marketplace.Application.Queries.Bookings;
using ParkingApp.Notifications.Application.Queries;
using ParkingApp.Notifications.Application.Commands;
using ParkingApp.Application.Interfaces;
using System.Linq.Expressions;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Identity.Application.Commands.Users;
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
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Marketplace.Contracts;
using ParkingApp.Messaging.Contracts;

namespace ParkingApp.UnitTests;

public class UserModuleTests
{
    private readonly Mock<IIdentityUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IVehicleRepository> _mockVehicleRepository;
    private readonly Mock<IDeviceTokenRepository> _mockDeviceTokenRepository;
    private readonly Mock<IMarketplaceUserDataCleanup> _mockMarketplaceCleanup;
    private readonly Mock<IMessagingUserDataCleanup> _mockMessagingCleanup;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<UpdateUserHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteUserHandler>> _mockDeleteLogger;

    public UserModuleTests()
    {
        _mockUnitOfWork = new Mock<IIdentityUnitOfWork>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockVehicleRepository = new Mock<IVehicleRepository>();
        _mockDeviceTokenRepository = new Mock<IDeviceTokenRepository>();
        _mockMarketplaceCleanup = new Mock<IMarketplaceUserDataCleanup>();
        _mockMessagingCleanup = new Mock<IMessagingUserDataCleanup>();
        _mockCache = new Mock<ICacheService>();
        _mockUpdateLogger = new Mock<ILogger<UpdateUserHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteUserHandler>>();

        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);
        _mockUnitOfWork.Setup(u => u.Vehicles).Returns(_mockVehicleRepository.Object);
        _mockUnitOfWork.Setup(u => u.DeviceTokens).Returns(_mockDeviceTokenRepository.Object);

        _mockVehicleRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vehicle, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle>());
        _mockDeviceTokenRepository.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DeviceToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceToken>());
    }

    [Fact]
    public async Task GetCurrentUserHandler_WhenInCache_ShouldReturnCachedData()
    {
        var handler = new GetCurrentUserHandler(_mockUnitOfWork.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var cachedUser = new UserDto(userId, "cached@test.com", "Cached", "User", "12345", ParkingApp.Identity.Domain.Enums.UserRole.User, true, true, DateTime.UtcNow);

        _mockCache.Setup(c => c.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedUser);

        var result = await handler.HandleAsync(new GetCurrentUserQuery(userId));

        result.Success.Should().BeTrue();
        result.Data.Should().BeEquivalentTo(cachedUser);
        _mockUserRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateUserHandler_WithValidData_ShouldUpdateAndInvalidateCache()
    {
        var handler = new UpdateUserHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockUpdateLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var dto = new UpdateUserDto("New", "Profile", "9876543210");
        var result = await handler.HandleAsync(new UpdateUserCommand(userId, dto));

        result.Success.Should().BeTrue();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Profile");
        _mockUserRepository.Verify(r => r.Update(user), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteUserHandler_WhenUserFound_ShouldDeleteAndRemoveFromCache()
    {
        var handler = new DeleteUserHandler(
            _mockUnitOfWork.Object,
            _mockMarketplaceCleanup.Object,
            _mockMessagingCleanup.Object,
            _mockCache.Object,
            _mockDeleteLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await handler.HandleAsync(new DeleteUserCommand(userId));

        result.Success.Should().BeTrue();
        _mockMarketplaceCleanup.Verify(c => c.StageDeleteForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMessagingCleanup.Verify(c => c.StageDeleteForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserRepository.Verify(r => r.HardDelete(user), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}








