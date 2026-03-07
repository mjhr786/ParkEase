using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.CQRS.Commands;

public class ParkingCommandsTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<CreateParkingHandler>> _mockCreateLogger;
    private readonly Mock<ILogger<UpdateParkingHandler>> _mockUpdateLogger;
    private readonly Mock<ILogger<DeleteParkingHandler>> _mockDeleteLogger;
    private readonly Mock<ILogger<ToggleActiveParkingHandler>> _mockToggleLogger;

    public ParkingCommandsTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockUserRepo = new Mock<IUserRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();

        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Users).Returns(_mockUserRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);

        _mockCache = new Mock<ICacheService>();
        _mockCreateLogger = new Mock<ILogger<CreateParkingHandler>>();
        _mockUpdateLogger = new Mock<ILogger<UpdateParkingHandler>>();
        _mockDeleteLogger = new Mock<ILogger<DeleteParkingHandler>>();
        _mockToggleLogger = new Mock<ILogger<ToggleActiveParkingHandler>>();
    }

    // CreateParkingHandler Tests
    [Fact]
    public async Task CreateParkingHandler_ShouldFail_WhenOwnerNotFound()
    {
        var handler = new CreateParkingHandler(_mockUow.Object, _mockCache.Object, _mockCreateLogger.Object);
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User)null);

        var res = await handler.HandleAsync(new CreateParkingCommand(Guid.NewGuid(), new CreateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, default, default, false, null, null, null, null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Owner not found");
    }

    [Fact]
    public async Task CreateParkingHandler_ShouldFail_WhenNotVendor()
    {
        var handler = new CreateParkingHandler(_mockUow.Object, _mockCache.Object, _mockCreateLogger.Object);
        var user = new User { Role = UserRole.Member };
        _mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var res = await handler.HandleAsync(new CreateParkingCommand(Guid.NewGuid(), new CreateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, default, default, false, null, null, null, null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Only vendors");
    }

    [Fact]
    public async Task CreateParkingHandler_ShouldSucceed()
    {
        var handler = new CreateParkingHandler(_mockUow.Object, _mockCache.Object, _mockCreateLogger.Object);
        var owner = new User { Id = Guid.NewGuid(), Role = UserRole.Vendor };
        _mockUserRepo.Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var res = await handler.HandleAsync(new CreateParkingCommand(owner.Id, new CreateParkingSpaceDto("T", "D", "A", "C", "S", "C", "P", 1, 1, ParkingType.Open, 1, 1, 1, 1, 1, default, default, false, null, null, null, null)));

        res.Success.Should().BeTrue();
        _mockParkingRepo.Verify(r => r.AddAsync(It.IsAny<ParkingSpace>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // UpdateParkingHandler Tests
    [Fact]
    public async Task UpdateParkingHandler_ShouldFail_WhenParkingNotFound()
    {
        var handler = new UpdateParkingHandler(_mockUow.Object, _mockCache.Object, _mockUpdateLogger.Object);
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ParkingSpace)null);

        var res = await handler.HandleAsync(new UpdateParkingCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateParkingSpaceDto("New T", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));

        res.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateParkingHandler_ShouldFail_WhenUnauthorized()
    {
        var handler = new UpdateParkingHandler(_mockUow.Object, _mockCache.Object, _mockUpdateLogger.Object);
        var parking = new ParkingSpace { OwnerId = Guid.NewGuid() };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new UpdateParkingCommand(Guid.NewGuid(), Guid.NewGuid(), new UpdateParkingSpaceDto("New T", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Unauthorized");
    }

    [Fact]
    public async Task UpdateParkingHandler_ShouldSucceed()
    {
        var handler = new UpdateParkingHandler(_mockUow.Object, _mockCache.Object, _mockUpdateLogger.Object);
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = ownerId, Title = "Old" };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new UpdateParkingCommand(parking.Id, ownerId, new UpdateParkingSpaceDto("New Title", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)));

        res.Success.Should().BeTrue();
        parking.Title.Should().Be("New Title");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // DeleteParkingHandler Tests
    [Fact]
    public async Task DeleteParkingHandler_ShouldFail_WhenActiveBookings()
    {
        var handler = new DeleteParkingHandler(_mockUow.Object, _mockCache.Object, _mockDeleteLogger.Object);
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = ownerId };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var res = await handler.HandleAsync(new DeleteParkingCommand(parking.Id, ownerId));

        res.Success.Should().BeFalse();
        res.Message.Should().Contain("Cannot delete");
    }

    [Fact]
    public async Task DeleteParkingHandler_ShouldSucceed()
    {
        var handler = new DeleteParkingHandler(_mockUow.Object, _mockCache.Object, _mockDeleteLogger.Object);
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = ownerId };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);
        _mockBookingRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Booking, bool>>>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var res = await handler.HandleAsync(new DeleteParkingCommand(parking.Id, ownerId));

        res.Success.Should().BeTrue();
        _mockParkingRepo.Verify(r => r.Remove(parking), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ToggleActiveParkingHandler Tests
    [Fact]
    public async Task ToggleActiveParkingHandler_ShouldSucceed()
    {
        var handler = new ToggleActiveParkingHandler(_mockUow.Object, _mockCache.Object, _mockToggleLogger.Object);
        var ownerId = Guid.NewGuid();
        var parking = new ParkingSpace { Id = Guid.NewGuid(), OwnerId = ownerId, IsActive = true };
        _mockParkingRepo.Setup(r => r.GetByIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(parking);

        var res = await handler.HandleAsync(new ToggleActiveParkingCommand(parking.Id, ownerId));

        res.Success.Should().BeTrue();
        parking.IsActive.Should().BeFalse();
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
