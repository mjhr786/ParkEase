using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.DTOs;

namespace ParkingApp.UnitTests.API;

public class DashboardControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly DashboardController _controller;
    private readonly Guid _userId;

    public DashboardControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _controller = new DashboardController(_mockDispatcher.Object);
        _userId = Guid.NewGuid();

        // Setup mock user
        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
        }, "mock"));

        _controller.ControllerContext = new ControllerContext()
        {
            HttpContext = new DefaultHttpContext() { User = user }
        };
    }

    [Fact]
    public async Task GetVendorDashboard_ShouldReturnOkWithData()
    {
        // Arrange
        var dashboardDto = new VendorDashboardDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, new List<BookingDto>(), new List<EarningsChartDataDto>());
        var apiResponse = new ApiResponse<VendorDashboardDto>(true, null, dashboardDto);
        
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetVendorDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _controller.GetVendorDashboard(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(apiResponse);
    }

    [Fact]
    public async Task GetMemberDashboard_ShouldReturnOkWithData()
    {
        // Arrange
        var dashboardDto = new MemberDashboardDto(0, 0, 0, 0, new List<BookingDto>(), new List<BookingDto>());
        var apiResponse = new ApiResponse<MemberDashboardDto>(true, null, dashboardDto);
        
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetMemberDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _controller.GetMemberDashboard(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(apiResponse);
    }
}
