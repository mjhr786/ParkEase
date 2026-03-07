using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.Application.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.Interfaces;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class DashboardServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<IParkingSpaceRepository> _mockParkingRepo;
    private readonly Mock<IBookingRepository> _mockBookingRepo;
    private readonly Mock<ICacheService> _mockCache;
    private readonly Mock<ILogger<DashboardService>> _mockLogger;
    private readonly DashboardService _service;

    public DashboardServiceTests()
    {
        _mockUow = new Mock<IUnitOfWork>();
        _mockParkingRepo = new Mock<IParkingSpaceRepository>();
        _mockBookingRepo = new Mock<IBookingRepository>();

        _mockUow.Setup(u => u.ParkingSpaces).Returns(_mockParkingRepo.Object);
        _mockUow.Setup(u => u.Bookings).Returns(_mockBookingRepo.Object);

        _mockCache = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<DashboardService>>();

        _service = new DashboardService(_mockUow.Object, _mockCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetVendorDashboardAsync_ShouldReturnFromCache()
    {
        var vid = Guid.NewGuid();
        var dto = new VendorDashboardDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new List<BookingDto>(), new List<DashboardChartDataDto>());
        _mockCache.Setup(c => c.GetAsync<VendorDashboardDto>($"dashboard:vendor:{vid}", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var res = await _service.GetVendorDashboardAsync(vid);

        res.Success.Should().BeTrue();
        res.Data.Should().BeSameAs(dto);
        _mockParkingRepo.Verify(r => r.GetByOwnerIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetVendorDashboardAsync_ShouldComputeAndCache()
    {
        var vid = Guid.NewGuid();
        _mockCache.Setup(c => c.GetAsync<VendorDashboardDto>($"dashboard:vendor:{vid}", It.IsAny<CancellationToken>())).ReturnsAsync((VendorDashboardDto?)null);

        var parking = new ParkingSpace { Id = Guid.NewGuid(), IsActive = true, AverageRating = 4.5, TotalReviews = 10 };
        _mockParkingRepo.Setup(r => r.GetByOwnerIdAsync(vid, It.IsAny<CancellationToken>())).ReturnsAsync(new List<ParkingSpace> { parking });
        
        var booking = new Booking { Id = Guid.NewGuid(), ParkingSpaceId = parking.Id, Status = BookingStatus.Completed, Payment = new Payment { Status = PaymentStatus.Completed }, TotalAmount = 100, CheckOutTime = DateTime.UtcNow };
        _mockBookingRepo.Setup(r => r.GetByParkingSpaceIdAsync(parking.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking> { booking });

        var res = await _service.GetVendorDashboardAsync(vid);

        res.Success.Should().BeTrue();
        res.Data!.TotalParkingSpaces.Should().Be(1);
        res.Data.TotalEarnings.Should().Be(100);
        _mockCache.Verify(c => c.SetAsync($"dashboard:vendor:{vid}", It.IsAny<VendorDashboardDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMemberDashboardAsync_ShouldReturnFromCache()
    {
        var mid = Guid.NewGuid();
        var dto = new MemberDashboardDto(0, 0, 0, 0, new List<BookingDto>(), new List<BookingDto>());
        _mockCache.Setup(c => c.GetAsync<MemberDashboardDto>($"dashboard:member:{mid}", It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var res = await _service.GetMemberDashboardAsync(mid);

        res.Success.Should().BeTrue();
        res.Data.Should().BeSameAs(dto);
        _mockBookingRepo.Verify(r => r.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetMemberDashboardAsync_ShouldComputeAndCache()
    {
        var mid = Guid.NewGuid();
        _mockCache.Setup(c => c.GetAsync<MemberDashboardDto>($"dashboard:member:{mid}", It.IsAny<CancellationToken>())).ReturnsAsync((MemberDashboardDto?)null);

        var booking = new Booking { Id = Guid.NewGuid(), UserId = mid, Status = BookingStatus.Completed, Payment = new Payment { Status = PaymentStatus.Completed }, TotalAmount = 100, CheckOutTime = DateTime.UtcNow };
        _mockBookingRepo.Setup(r => r.GetByUserIdAsync(mid, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking> { booking });

        var res = await _service.GetMemberDashboardAsync(mid);

        res.Success.Should().BeTrue();
        res.Data!.TotalBookings.Should().Be(1);
        res.Data.TotalSpent.Should().Be(100);
        _mockCache.Verify(c => c.SetAsync($"dashboard:member:{mid}", It.IsAny<MemberDashboardDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
