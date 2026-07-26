using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Marketplace.Application.Commands.EventPackages;
using ParkingApp.Marketplace.Application.Queries.EventPackages;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.UnitTests.API;

public class EventPackagesControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly EventPackagesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public EventPackagesControllerTests()
    {
        _controller = new EventPackagesController(_dispatcher.Object);
        SetUser(_userId);
    }

    private void SetUser(Guid? userId)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }

    [Fact]
    public async Task GetOnSale_ReturnsOk()
    {
        var response = new ApiResponse<List<EventParkingPackageDto>>(true, null, new List<EventParkingPackageDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetOnSaleEventPackagesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetOnSale(20, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task GetById_WhenMissing_ReturnsNotFound()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetEventPackageByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventParkingPackageDto>(false, "not found", null));

        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMine_WhenNoUser_ReturnsUnauthorized()
    {
        SetUser(null);

        var result = await _controller.GetMine(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Create_WhenSuccess_ReturnsCreated()
    {
        var dto = new CreateEventParkingPackageDto(
            Guid.NewGuid(),
            "Concert",
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow.AddDays(1).AddHours(4),
            500m,
            50);

        var response = new ApiResponse<EventParkingPackageDto>(true, null, null);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateEventParkingPackageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Purchase_WhenFails_ReturnsBadRequest()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<PurchaseEventParkingPackageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<BookingDto>(false, "sold out", null));

        var result = await _controller.Purchase(
            Guid.NewGuid(),
            new PurchaseEventParkingPackageDto(VehicleType.Car, "KA01AB1234"),
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Deactivate_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<DeactivateEventParkingPackageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.Deactivate(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAnalytics_WhenUnauthorized_ReturnsForbid()
    {
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetEventPackageAnalyticsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<EventPackageAnalyticsDto>(false, "Unauthorized", null));

        var result = await _controller.GetAnalytics(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }
}
