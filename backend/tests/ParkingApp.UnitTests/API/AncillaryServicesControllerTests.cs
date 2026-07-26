using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Ancillary;
using ParkingApp.Marketplace.Application.Queries.Ancillary;
using ParkingApp.Marketplace.Contracts.DTOs;

namespace ParkingApp.UnitTests.API;

public class AncillaryServicesControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly AncillaryServicesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public AncillaryServicesControllerTests()
    {
        _controller = new AncillaryServicesController(_dispatcher.Object);
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
    public async Task GetByParking_ReturnsOk()
    {
        var spaceId = Guid.NewGuid();
        var response = new ApiResponse<List<ParkingAncillaryServiceDto>>(true, null, new List<ParkingAncillaryServiceDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetAncillaryServicesForParkingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetByParking(spaceId, true, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
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
        var dto = new CreateParkingAncillaryServiceDto(Guid.NewGuid(), "Wash", 199m, "Car wash");
        var response = new ApiResponse<ParkingAncillaryServiceDto>(true, null, null);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateParkingAncillaryServiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_WhenUnauthorized_ReturnsForbid()
    {
        var dto = new CreateParkingAncillaryServiceDto(Guid.NewGuid(), "Wash", 100m);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateParkingAncillaryServiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAncillaryServiceDto>(false, "Unauthorized", null));

        var result = await _controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var dto = new UpdateParkingAncillaryServiceDto(Name: "Wash", Price: 150m);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<UpdateParkingAncillaryServiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<ParkingAncillaryServiceDto>(false, "Add-on service not found", null));

        var result = await _controller.Update(Guid.NewGuid(), dto, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Deactivate_WhenSuccess_ReturnsOk()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<DeactivateParkingAncillaryServiceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(true, null, true));

        var result = await _controller.Deactivate(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
