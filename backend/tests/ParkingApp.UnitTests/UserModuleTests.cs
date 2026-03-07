using System.Linq.Expressions;
using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests;

public class UserModuleTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<UpdateUserHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteUserHandler>> _mockDeleteLogger;

    public UserModuleTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockUserRepository = new Mock<IUserRepository>();
        _mockCache = new Mock<ICacheService>();
        _mockUpdateLogger = new Mock<ILogger<UpdateUserHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteUserHandler>>();

        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepository.Object);

        // Setup related repositories for delete handler
        var mockBookingRepo = new Mock<IBookingRepository>();
        var mockPaymentRepo = new Mock<IPaymentRepository>();
        var mockReviewRepo = new Mock<IReviewRepository>();
        var mockFavoriteRepo = new Mock<IFavoriteRepository>();
        var mockNotificationRepo = new Mock<INotificationRepository>();
        var mockVehicleRepo = new Mock<IVehicleRepository>();
        var mockConversationRepo = new Mock<IConversationRepository>();
        var mockChatMessageRepo = new Mock<IChatMessageRepository>();

        mockBookingRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Booking>());
        mockReviewRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Review, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Review>());
        mockFavoriteRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Favorite, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Favorite>());
        mockNotificationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Notification, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Notification>());
        mockVehicleRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vehicle, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Vehicle>());
        mockConversationRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Conversation, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>());

        _mockUnitOfWork.Setup(u => u.Bookings).Returns(mockBookingRepo.Object);
        _mockUnitOfWork.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        _mockUnitOfWork.Setup(u => u.Reviews).Returns(mockReviewRepo.Object);
        _mockUnitOfWork.Setup(u => u.Favorites).Returns(mockFavoriteRepo.Object);
        _mockUnitOfWork.Setup(u => u.Notifications).Returns(mockNotificationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Vehicles).Returns(mockVehicleRepo.Object);
        _mockUnitOfWork.Setup(u => u.Conversations).Returns(mockConversationRepo.Object);
        _mockUnitOfWork.Setup(u => u.ChatMessages).Returns(mockChatMessageRepo.Object);
    }

    [Fact]
    public async Task GetCurrentUserHandler_WhenInCache_ShouldReturnCachedData()
    {
        var handler = new GetCurrentUserHandler(_mockUnitOfWork.Object, _mockCache.Object);
        var userId = Guid.NewGuid();
        var cachedUser = new UserDto(userId, "cached@test.com", "Cached", "User", "12345", UserRole.Member, true, true, DateTime.UtcNow);

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
        var user = new User { Id = userId, FirstName = "Old", LastName = "Name", Email = "test@test.com" };

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
        var handler = new DeleteUserHandler(_mockUnitOfWork.Object, _mockCache.Object, _mockDeleteLogger.Object);
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await handler.HandleAsync(new DeleteUserCommand(userId));

        result.Success.Should().BeTrue();
        _mockUserRepository.Verify(r => r.HardDelete(user), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
