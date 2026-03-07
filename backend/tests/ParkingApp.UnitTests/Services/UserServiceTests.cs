using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<UserService>> _mockLogger;
    private readonly UserService _service;

    public UserServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<UserService>>();

        _service = new UserService(_mockUow.Object, _mockCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnFromCache_WhenCached()
    {
        var userId = Guid.NewGuid();
        var cachedDto = new UserDto(userId, "t", "r", "F", "L", ParkingApp.Domain.Enums.UserRole.Member, true, true, DateTime.UtcNow);
        _mockCache.Setup(c => c.GetAsync<UserDto>($"user:{userId}", It.IsAny<CancellationToken>())).ReturnsAsync(cachedDto);

        var result = await _service.GetByIdAsync(userId);

        result.Success.Should().BeTrue();
        result.Data.Should().Be(cachedDto);
        _mockUserRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldFail_WhenUserNotFoundInDb()
    {
        var userId = Guid.NewGuid();
        _mockCache.Setup(c => c.GetAsync<UserDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserDto?)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.GetByIdAsync(userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldFetchFromDbAndCache_WhenNotCached()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        _mockCache.Setup(c => c.GetAsync<UserDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((UserDto?)null);
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.GetByIdAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.Id.Should().Be(userId);
        _mockCache.Verify(c => c.SetAsync($"user:{userId}", It.IsAny<UserDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldFail_WhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.UpdateAsync(userId, new UpdateUserDto("First", "Last", "Phone"));

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndInvalidateCache()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, FirstName = "Old", LastName = "Old", PhoneNumber = "Old" };
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.UpdateAsync(userId, new UpdateUserDto("NewFirst", "NewLast", "NewPhone"));

        result.Success.Should().BeTrue();
        user.FirstName.Should().Be("NewFirst");
        user.LastName.Should().Be("NewLast");
        user.PhoneNumber.Should().Be("NewPhone");
        _mockUserRepo.Verify(r => r.Update(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_ShouldFail_WhenUserNotFound()
    {
        var userId = Guid.NewGuid();
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await _service.DeleteAsync(userId);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User not found");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveAndInvalidateCache()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId };
        _mockUserRepo.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await _service.DeleteAsync(userId);

        result.Success.Should().BeTrue();
        _mockUserRepo.Verify(r => r.Remove(user), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCache.Verify(c => c.RemoveAsync($"user:{userId}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
