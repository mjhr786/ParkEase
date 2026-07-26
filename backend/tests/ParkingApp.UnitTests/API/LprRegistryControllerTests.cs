using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.Lpr;
using ParkingApp.Marketplace.Application.Queries.Lpr;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class LprRegistryControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly LprRegistryController _controller;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _spaceId = Guid.NewGuid();

    public LprRegistryControllerTests()
    {
        _controller = new LprRegistryController(_dispatcher.Object);
        SetUser(_userId, isAdmin: false);
    }

    private void SetUser(Guid? userId, bool isAdmin)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "mock"))
            }
        };
    }

    [Fact]
    public async Task ListCameraKeys_WhenUnauthorizedActor_ReturnsUnauthorized()
    {
        SetUser(null, isAdmin: false);

        var result = await _controller.ListCameraKeys(_spaceId, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ListCameraKeys_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<IReadOnlyList<LprCameraKeyDto>>(true, null, Array.Empty<LprCameraKeyDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetLprCameraKeysQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.ListCameraKeys(_spaceId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateCameraKey_WhenSuccess_Returns201()
    {
        var created = new LprCameraKeyCreatedDto(
            Guid.NewGuid(), _spaceId, "Gate", "key-1", "secret", "secr", true, DateTime.UtcNow);
        var response = new ApiResponse<LprCameraKeyCreatedDto>(true, null, created);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateLprCameraKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.CreateCameraKey(
            _spaceId,
            new CreateLprCameraKeyRequest("Gate"),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateCameraKey_WhenUnauthorizedMessage_Returns403()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateLprCameraKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<LprCameraKeyCreatedDto>(false, "Unauthorized", null));

        var result = await _controller.CreateCameraKey(
            _spaceId,
            new CreateLprCameraKeyRequest("Gate"),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task ListPlateRules_WhenSuccess_ReturnsOk()
    {
        var response = new ApiResponse<IReadOnlyList<LprPlateRuleDto>>(true, null, Array.Empty<LprPlateRuleDto>());
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetLprPlateRulesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.ListPlateRules(_spaceId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreatePlateRule_WhenSuccess_Returns201()
    {
        var rule = new LprPlateRuleDto(
            Guid.NewGuid(), _spaceId, "KA01AB1234", LprPlateRuleType.Allow, null, true, DateTime.UtcNow);
        var response = new ApiResponse<LprPlateRuleDto>(true, null, rule);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateLprPlateRuleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.CreatePlateRule(
            _spaceId,
            new CreateLprPlateRuleRequest("KA01AB1234", LprPlateRuleType.Allow),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task DeleteCameraKey_WhenNotFound_Returns404()
    {
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<DeleteLprCameraKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<bool>(false, "Parking space not found", false));

        var result = await _controller.DeleteCameraKey(_spaceId, Guid.NewGuid(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task SetCameraKeyEnabled_WhenSuccess_ReturnsOk()
    {
        var key = new LprCameraKeyDto(
            Guid.NewGuid(), _spaceId, "Gate", "key-1", "secr", false, DateTime.UtcNow);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<SetLprCameraKeyEnabledCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<LprCameraKeyDto>(true, null, key));

        var result = await _controller.SetCameraKeyEnabled(
            _spaceId,
            Guid.NewGuid(),
            new LprRegistryController.SetEnabledRequest(false),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
