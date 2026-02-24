using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Logging;
using ParkingApp.Notifications.Services;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Entities;

namespace ParkingApp.UnitTests.Notifications;

public class NotificationCoordinatorTests
{
    private readonly Mock<INotificationService> _mockInAppService;
    private readonly Mock<ISmsNotificationService> _mockSmsService;
    private readonly Mock<IPushNotificationService> _mockPushService;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<INotificationRepository> _mockNotificationRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ILogger<NotificationCoordinator>> _mockLogger;

    public NotificationCoordinatorTests()
    {
        _mockInAppService = new Mock<INotificationService>();
        _mockSmsService = new Mock<ISmsNotificationService>();
        _mockPushService = new Mock<IPushNotificationService>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockNotificationRepo = new Mock<INotificationRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockLogger = new Mock<ILogger<NotificationCoordinator>>();

        _mockUnitOfWork.Setup(u => u.Notifications).Returns(_mockNotificationRepo.Object);
        _mockUnitOfWork.Setup(u => u.Users).Returns(_mockUserRepo.Object);
    }

    [Fact]
    public async Task SendAsync_ShouldSaveToDatabaseFirst()
    {
        // Arrange
        var coordinator = new NotificationCoordinator(
            _mockInAppService.Object,
            _mockSmsService.Object,
            _mockPushService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);

        var userId = Guid.NewGuid();
        var request = new NotificationRequest("SystemAlert", "Test", "Msg", NotificationChannels.None);

        // Act
        await coordinator.SendAsync(userId, request);

        // Assert
        _mockNotificationRepo.Verify(r => r.AddAsync(It.Is<Notification>(n => n.UserId == userId && n.Title == "Test"), It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithInAppChannel_ShouldCallSignalRService()
    {
        // Arrange
        var coordinator = new NotificationCoordinator(
            _mockInAppService.Object,
            _mockSmsService.Object,
            _mockPushService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);

        var userId = Guid.NewGuid();
        var request = new NotificationRequest("Alert", "Title", "Message", NotificationChannels.InApp);

        // Act
        await coordinator.SendAsync(userId, request);

        // Assert
        _mockInAppService.Verify(s => s.NotifyUserAsync(userId, It.Is<NotificationDto>(dto => dto.Title == "Title"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithMultipleChannels_ShouldTriggerAllRequestedServices()
    {
        // Arrange
        var coordinator = new NotificationCoordinator(
            _mockInAppService.Object,
            _mockSmsService.Object,
            _mockPushService.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);

        var userId = Guid.NewGuid();
        var request = new NotificationRequest("Alert", "Multi", "Msg", NotificationChannels.InApp | NotificationChannels.Sms, null, NotificationPriority.High);

        _mockUserRepo.As<IRepository<User>>().Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, PhoneNumber = "+1234567890" });

        _mockSmsService.Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsResult(true));

        // Act
        await coordinator.SendAsync(userId, request);

        // Assert
        _mockInAppService.Verify(s => s.NotifyUserAsync(userId, It.IsAny<NotificationDto>(), It.IsAny<CancellationToken>()), Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error || l == LogLevel.Warning),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never);
            
        _mockSmsService.Verify(s => s.SendAsync(It.IsAny<string>(), "Multi: Msg", It.IsAny<CancellationToken>()), Times.Once); 
    }
}
