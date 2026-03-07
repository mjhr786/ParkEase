using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class ParkingControllerTests
{
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IValidator<CreateParkingSpaceDto>> _createValidatorMock;
    private readonly ParkingController _controller;

    public ParkingControllerTests()
    {
        _dispatcherMock = new Mock<IDispatcher>();
        _createValidatorMock = new Mock<IValidator<CreateParkingSpaceDto>>();
        _controller = new ParkingController(_dispatcherMock.Object, _createValidatorMock.Object);
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId, string role = "Vendor")
    {
        var claims = new[] 
        { 
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var claimsPrincipal = new ClaimsPrincipal(identity);
        
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task GetById_ReturnsOk()
    {
        var id = Guid.NewGuid();
        _dispatcherMock.Setup(d => d.QueryAsync(It.Is<GetParkingByIdQuery>(q => q.ParkingId == id), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ParkingSpaceDto>(true, "Success", new ParkingSpaceDto(id, Guid.NewGuid(), "Owner", "Title", "Desc", "Address", "City", "State", "Country", "12345", 0, 0, ParkingApp.Domain.Enums.ParkingType.Garage, 10, 10, 10m, 10m, 10m, 10m, TimeSpan.Zero, TimeSpan.Zero, true, new List<string>(), new List<ParkingApp.Domain.Enums.VehicleType>(), new List<string>(), true, true, 5.0, 10, null, DateTime.UtcNow, null), null)));

        var result = await _controller.GetById(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_ReturnsOk()
    {
         var dto = new ParkingSearchDto();
         _dispatcherMock.Setup(d => d.QueryAsync(It.Is<SearchParkingQuery>(q => q.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ParkingSearchResultDto>(true, "Success", new ParkingSearchResultDto(new List<ParkingSpaceDto>(), 0, 1, 10, 1), null)));

        var result = await _controller.Search(dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMap_ReturnsOk()
    {
         var dto = new ParkingSearchDto();
         _dispatcherMock.Setup(d => d.QueryAsync(It.Is<GetMapCoordinatesQuery>(q => q.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<List<ParkingMapDto>>(true, "Success", new List<ParkingMapDto>(), null)));

        var result = await _controller.GetMap(dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyListings_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        
        _dispatcherMock.Setup(d => d.QueryAsync(It.Is<GetOwnerParkingsQuery>(q => q.OwnerId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<List<ParkingSpaceDto>>(true, "Success", new List<ParkingSpaceDto>(), null)));

        var result = await _controller.GetMyListings(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new CreateParkingSpaceDto("Title", "Desc", "Address", "City", "State", "Country", "12345", 0, 0, ParkingApp.Domain.Enums.ParkingType.Garage, 10, 10m, 10m, 10m, 10m, null, null, true, null, null, null, null);
        var id = Guid.NewGuid();

        _createValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _dispatcherMock.Setup(d => d.SendAsync(It.IsAny<CreateParkingCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ParkingSpaceDto>(true, "Success", new ParkingSpaceDto(id, Guid.NewGuid(), "Owner", "Title", "Desc", "Address", "City", "State", "Country", "12345", 0, 0, ParkingApp.Domain.Enums.ParkingType.Garage, 10, 10, 10m, 10m, 10m, 10m, TimeSpan.Zero, TimeSpan.Zero, true, new List<string>(), new List<ParkingApp.Domain.Enums.VehicleType>(), new List<string>(), true, true, 5.0, 10, null, DateTime.UtcNow, null), null)));

        var result = await _controller.Create(dto, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Contain(id.ToString());
    }

    [Fact]
    public async Task Update_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new UpdateParkingSpaceDto("Updated name", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

         _dispatcherMock.Setup(d => d.SendAsync(It.Is<UpdateParkingCommand>(c => c.ParkingId == id && c.OwnerId == userId && c.Dto == dto), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<ParkingSpaceDto>(true, "Success", new ParkingSpaceDto(id, Guid.NewGuid(), "Owner", "Title", "Desc", "Address", "City", "State", "Country", "12345", 0, 0, ParkingApp.Domain.Enums.ParkingType.Garage, 10, 10, 10m, 10m, 10m, 10m, TimeSpan.Zero, TimeSpan.Zero, true, new List<string>(), new List<ParkingApp.Domain.Enums.VehicleType>(), new List<string>(), true, true, 5.0, 10, null, DateTime.UtcNow, null), null)));

        var result = await _controller.Update(id, dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

     [Fact]
    public async Task Delete_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

         _dispatcherMock.Setup(d => d.SendAsync(It.Is<DeleteParkingCommand>(c => c.ParkingId == id && c.OwnerId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        var result = await _controller.Delete(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

     [Fact]
    public async Task ToggleActive_ReturnsOk()
    {
         var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

         _dispatcherMock.Setup(d => d.SendAsync(It.Is<ToggleActiveParkingCommand>(c => c.ParkingId == id && c.OwnerId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<bool>(true, "Success", true, null)));

        var result = await _controller.ToggleActive(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
