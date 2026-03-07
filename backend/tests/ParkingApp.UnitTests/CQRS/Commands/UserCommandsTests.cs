using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class UserCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<IPaymentRepository> _mockPaymentRepo;
    private readonly Mock<IReviewRepository> _mockReviewRepo;
    private readonly Mock<IFavoriteRepository> _mockFavoriteRepo;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IVehicleRepository> _mockVehicleRepo;
    private readonly Mock<IConversationRepository> _mockConversationRepo;
    private readonly Mock<IChatMessageRepository> _mockChatMessageRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<UpdateUserHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteUserHandler>> _mockDeleteLogger;

    public UserCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();
        _mockPaymentRepo = new Mock<IPaymentRepository>();
        _mockReviewRepo = new Mock<IReviewRepository>();
        _mockFavoriteRepo = new Mock<IFavoriteRepository>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockVehicleRepo = new Mock<IVehicleRepository>();
        _mockConversationRepo = new Mock<IConversationRepository>();
        _mockChatMessageRepo = new Mock<IChatMessageRepository>();

        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);
        _mockUow.Setup(u => u.Payments).Returns(_mockPaymentRepo.Object);
        _mockUow.Setup(u => u.Reviews).Returns(_mockReviewRepo.Object);
        _mockUow.Setup(u => u.Favorites).Returns(_mockFavoriteRepo.Object);
        _mockUow.Setup(u => u.Notifications).Returns(_mockNotificationRepo.Object);
        _mockUow.Setup(u => u.Vehicles).Returns(_mockVehicleRepo.Object);
        _mockUow.Setup(u => u.Conversations).Returns(_mockConversationRepo.Object);
        _mockUow.Setup(u => u.ChatMessages).Returns(_mockChatMessageRepo.Object);

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
        var dto = new UserDto(userId, "t", "r", "F", "L", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow);
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
        var user = new User { Id = userId, FirstName = "Test" };
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
        var user = new User { Id = userId };
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
        var handler = new DeleteUserHandler(_mockUow.Object, _mockCache.Object, _mockDeleteLogger.Object);
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new DeleteUserCommand(Guid.NewGuid()));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUserHandler_ShouldHardDeleteUserAndRelatedEntities()
    {
        var handler = new DeleteUserHandler(_mockUow.Object, _mockCache.Object, _mockDeleteLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        // Setup empty related entities
        _mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());
        _mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Review, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review>());
        _mockFavoriteRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Favorite, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Favorite>());
        _mockNotificationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Notification, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());
        _mockVehicleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vehicle, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle>());
        _mockConversationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());

        var res = await handler.HandleAsync(new DeleteUserCommand(userId));

        res.Success.Should().BeTrue();
        _mockUserRepo.Verify(r => r.HardDelete(user), Times.Once);
        _mockUow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
