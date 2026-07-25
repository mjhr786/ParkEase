using ParkingApp.Application.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
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
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class UserCommandsTests
{
    private readonly Mock<IIdentityUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IVehicleRepository> _mockVehicleRepo;
    private readonly Mock<IDeviceTokenRepository> _mockDeviceTokenRepo;
    private readonly Mock<IMarketplaceUserDataCleanup> _mockMarketplaceCleanup;
    private readonly Mock<IMessagingUserDataCleanup> _mockMessagingCleanup;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<UpdateUserHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteUserHandler>> _mockDeleteLogger;

    public UserCommandsTests()
    {
        _mockUow = new Mock<IIdentityUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockVehicleRepo = new Mock<IVehicleRepository>();
        _mockDeviceTokenRepo = new Mock<IDeviceTokenRepository>();
        _mockMarketplaceCleanup = new Mock<IMarketplaceUserDataCleanup>();
        _mockMessagingCleanup = new Mock<IMessagingUserDataCleanup>();

        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.Vehicles).Returns(_mockVehicleRepo.Object);
        _mockUow.Setup(u => u.DeviceTokens).Returns(_mockDeviceTokenRepo.Object);

        _mockCache = new Mock<ICacheService>();
        _mockUpdateLogger = new Mock<ILogger<UpdateUserHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteUserHandler>>();
    }

    // GetCurrentUserHandler Tests
    [Fact]
    public async Task GetCurrentUserHandler_ShouldReturnFromCache()
    {
        var handler = new GetCurrentUserHandler(_mockUow.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var dto = new UserDto(userId, "t", "r", "F", "L", ParkingApp.Identity.Domain.Enums.UserRole.User, true, true, DateTime.UtcNow);
        _mockCache.Setup(c => c.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var res = await handler.HandleAsync(new GetCurrentUserQuery(userId));

        res.Success.Should().BeTrue();
        res.Data.Should().BeSameAs(dto);
        _mockUserRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserHandler_ShouldFetchAndCache_WhenNotCached()
    {
        var handler = new GetCurrentUserHandler(_mockUow.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        _mockCache.Setup(c => c.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>())).ReturnsAsync((UserDto)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new GetCurrentUserQuery(userId));

        res.Success.Should().BeTrue();
        res.Data.Id.Should().Be(userId);
        _mockCache.Verify(c => c.SetAsync($"user:{userId}", It.IsAny<UserDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // UpdateUserHandler Tests
    [Fact]
    public async Task UpdateUserHandler_ShouldFail_WhenUserNotFound()
    {
        var handler = new UpdateUserHandler(_mockUow.Object, _mockCache.Object, _mockUpdateLogger.Object);
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new UpdateUserCommand(Guid.NewGuid(), new UpdateUserDto("F", "L", "555")));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateUserHandler_ShouldSucceed()
    {
        var handler = new UpdateUserHandler(_mockUow.Object, _mockCache.Object, _mockUpdateLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new UpdateUserCommand(userId, new UpdateUserDto("F", "L", "555")));

        res.Success.Should().BeTrue();
        user.FirstName.Should().Be("F");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    // DeleteUserHandler Tests
    [Fact]
    public async Task DeleteUserHandler_ShouldFail_WhenUserNotFound()
    {
        var handler = new DeleteUserHandler(
            _mockUow.Object,
            _mockMarketplaceCleanup.Object,
            _mockMessagingCleanup.Object,
            _mockCache.Object,
            _mockDeleteLogger.Object);
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new DeleteUserCommand(Guid.NewGuid()));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserHandler_ShouldHardDeleteUserAndRelatedEntities()
    {
        var handler = new DeleteUserHandler(
            _mockUow.Object,
            _mockMarketplaceCleanup.Object,
            _mockMessagingCleanup.Object,
            _mockCache.Object,
            _mockDeleteLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = Guid.NewGuid(), Email = "test@example.com", PasswordHash = "hash", FirstName = "Test", LastName = "User", PhoneNumber = "1", IsActive = true };
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        _mockVehicleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vehicle, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle>());
        _mockDeviceTokenRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<DeviceToken, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceToken>());

        var res = await handler.HandleAsync(new DeleteUserCommand(userId));

        res.Success.Should().BeTrue();
        _mockMarketplaceCleanup.Verify(c => c.StageDeleteForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockMessagingCleanup.Verify(c => c.StageDeleteForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserRepo.Verify(r => r.HardDelete(user), Times.Once);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }
}






