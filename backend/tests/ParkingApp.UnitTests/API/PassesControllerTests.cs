using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Application.Commands.ParkingPasses;
using ParkingApp.Marketplace.Application.Queries.ParkingPasses;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.UnitTests.API;

public class PassesControllerTests
{
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<IValidator<CreateParkingPassDto>> _createValidator = new();
    private readonly Mock<IValidator<AssignCorporatePassDto>> _corporateValidator = new();
    private readonly PassesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public PassesControllerTests()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateParkingPassDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _corporateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<AssignCorporatePassDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _controller = new PassesController(
            _dispatcher.Object,
            _createValidator.Object,
            _corporateValidator.Object);

        SetUser(_userId, isAdmin: false);
    }

    private void SetUser(Guid? userId, bool isAdmin)
    {
        var claims = new List<Claim>();
        if (userId is not null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()));
        if (isAdmin)
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var identity = new ClaimsIdentity(claims, "mock");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetMyActivePasses_WhenAuthenticated_ReturnsOk()
    {
        var response = new ApiResponse<ActiveParkingPassesDto>(
            true, null, new ActiveParkingPassesDto(false, new List<ParkingPassDto>()));
        _dispatcher
            .Setup(d => d.QueryAsync(It.IsAny<GetUserActivePassQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetMyActivePasses(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
        _dispatcher.Verify(d => d.QueryAsync(
            It.Is<GetUserActivePassQuery>(q => q.UserId == _userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetMyActivePasses_WhenNoUser_ReturnsUnauthorized()
    {
        SetUser(null, isAdmin: false);

        var result = await _controller.GetMyActivePasses(CancellationToken.None);

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Create_WhenValidationFails_ReturnsBadRequest()
    {
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateParkingPassDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("PassType", "Required") }));

        var dto = new CreateParkingPassDto(
            PassTypeKind.Monthly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            null,
            null,
            PassUsageMode.UnlimitedEntries,
            null,
            10);

        var result = await _controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _dispatcher.Verify(d => d.SendAsync(It.IsAny<CreateParkingPassCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WhenValid_ReturnsCreated()
    {
        var dto = new CreateParkingPassDto(
            PassTypeKind.Monthly,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(30),
            Guid.NewGuid(),
            null,
            PassUsageMode.UnlimitedEntries,
            null,
            15);

        var response = new ApiResponse<ParkingPassDto>(true, "created", null);
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<CreateParkingPassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Create(dto, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task AssignCorporate_WhenValid_ReturnsOk()
    {
        SetUser(_userId, isAdmin: true);
        var dto = new AssignCorporatePassDto(
            new[] { Guid.NewGuid() },
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null,
            "ZONE-A",
            PassUsageMode.UnlimitedEntries,
            null,
            20,
            "batch-1");

        var response = new ApiResponse<CorporatePassAssignmentResultDto>(
            true, null, new CorporatePassAssignmentResultDto("batch-1", 1, new List<ParkingPassDto>()));
        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<AssignCorporatePassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.AssignCorporate(dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(response);
    }

    [Fact]
    public async Task AssignCorporate_WhenHandlerFails_ReturnsBadRequest()
    {
        SetUser(_userId, isAdmin: true);
        var dto = new AssignCorporatePassDto(
            new[] { Guid.NewGuid() },
            DateTime.UtcNow,
            DateTime.UtcNow.AddMonths(1),
            null,
            null,
            PassUsageMode.UnlimitedEntries,
            null,
            10);

        _dispatcher
            .Setup(d => d.SendAsync(It.IsAny<AssignCorporatePassCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<CorporatePassAssignmentResultDto>(false, "failed", null));

        var result = await _controller.AssignCorporate(dto, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
