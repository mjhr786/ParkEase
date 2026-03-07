using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Vehicles;
using ParkingApp.Application.CQRS.Queries.Vehicles;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.API;

public class VehiclesControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly VehiclesController _controller;
    private readonly Guid _userId;

    public VehiclesControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _controller = new VehiclesController(_mockDispatcher.Object);
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

    private void SetUnauthorizedUser()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    [Fact]
    public async Task GetMyVehicles_ShouldReturnOkWithData_WhenUserIsAuthorized()
    {
        // Arrange
        var vehicles = new List<VehicleDto> { new VehicleDto(Guid.NewGuid(), _userId, "TEST1", "Make", "Model", "Color", VehicleType.Car, true, DateTime.UtcNow) };
        var response = new ApiResponse<IEnumerable<VehicleDto>>(true, null, vehicles);
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetMyVehiclesQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        // Act
        var result = await _controller.GetMyVehicles();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(vehicles);
    }

    [Fact]
    public async Task GetMyVehicles_ShouldReturnUnauthorized_WhenUserIsNotAuthorized()
    {
        SetUnauthorizedUser();
        var result = await _controller.GetMyVehicles();
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AddVehicle_ShouldReturnCreatedAtAction_WhenCommandSucceeds()
    {
        var dto = new CreateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true);
        var vehicle = new VehicleDto(Guid.NewGuid(), _userId, "TEST1", "Make", "Model", "Color", VehicleType.Car, true, DateTime.UtcNow);
        var response = new ApiResponse<VehicleDto>(true, null, vehicle);

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<CreateVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.AddVehicle(dto);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(_controller.GetMyVehicles));
        createdResult.Value.Should().Be(vehicle);
    }

    [Fact]
    public async Task AddVehicle_ShouldReturnBadRequest_WhenCommandFails()
    {
        var dto = new CreateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true);
        var response = new ApiResponse<VehicleDto>(false, "Error", null);

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<CreateVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.AddVehicle(dto);

        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Error");
    }

    [Fact]
    public async Task AddVehicle_ShouldReturnUnauthorized_WhenUserIsNotAuthorized()
    {
        SetUnauthorizedUser();
        var result = await _controller.AddVehicle(new CreateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true));
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task UpdateVehicle_ShouldReturnOk_WhenCommandSucceeds()
    {
        var dto = new UpdateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true);
        var vehicle = new VehicleDto(Guid.NewGuid(), _userId, "TEST1", "Make", "Model", "Color", VehicleType.Car, true, DateTime.UtcNow);
        var response = new ApiResponse<VehicleDto>(true, null, vehicle);

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<UpdateVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.UpdateVehicle(vehicle.Id, dto);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(vehicle);
    }

    [Fact]
    public async Task UpdateVehicle_ShouldReturnBadRequest_WhenCommandFails()
    {
        var dto = new UpdateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true);
        var response = new ApiResponse<VehicleDto>(false, "Error", null);

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<UpdateVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.UpdateVehicle(Guid.NewGuid(), dto);

        var badResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badResult.Value.Should().Be("Error");
    }

    [Fact]
    public async Task UpdateVehicle_ShouldReturnUnauthorized_WhenUserIsNotAuthorized()
    {
        SetUnauthorizedUser();
        var result = await _controller.UpdateVehicle(Guid.NewGuid(), new UpdateVehicleDto("TEST1", "Make", "Model", "Color", VehicleType.Car, true));
        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteVehicle_ShouldReturnNoContent_WhenCommandSucceeds()
    {
        var response = new ApiResponse<bool>(true, null, true);
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<DeleteVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.DeleteVehicle(Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteVehicle_ShouldReturnBadRequest_WhenCommandFails()
    {
        var response = new ApiResponse<bool>(false, "Error", false);
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<DeleteVehicleCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await _controller.DeleteVehicle(Guid.NewGuid());

        var badResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badResult.Value.Should().Be("Error");
    }

    [Fact]
    public async Task DeleteVehicle_ShouldReturnUnauthorized_WhenUserIsNotAuthorized()
    {
        SetUnauthorizedUser();
        var result = await _controller.DeleteVehicle(Guid.NewGuid());
        result.Should().BeOfType<UnauthorizedResult>();
    }
}
