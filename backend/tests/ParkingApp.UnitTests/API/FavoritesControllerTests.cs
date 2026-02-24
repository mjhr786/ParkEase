using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Queries.Favorites;
using ParkingApp.Application.CQRS.Commands.Favorites;
using ParkingApp.Application.DTOs;

namespace ParkingApp.UnitTests.API;

public class FavoritesControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly FavoritesController _controller;
    private readonly Guid _userId;

    public FavoritesControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _controller = new FavoritesController(_mockDispatcher.Object);
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
    public async Task GetMyFavorites_ShouldReturnOkWithFavorites()
    {
        // Arrange
        var favorites = new List<ParkingSpaceDto>();
        var apiResponse = new ApiResponse<IEnumerable<ParkingSpaceDto>>(true, null, favorites);
        
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetMyFavoritesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _controller.GetMyFavorites(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(apiResponse);
    }

    [Fact]
    public async Task ToggleFavorite_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var parkingSpaceId = Guid.NewGuid();
        var apiResponse = new ApiResponse<bool>(true, "Success", true);
        
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<ToggleFavoriteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _controller.ToggleFavorite(parkingSpaceId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(apiResponse);
    }

    [Fact]
    public async Task ToggleFavorite_WhenFailed_ShouldReturnBadRequest()
    {
        // Arrange
        var parkingSpaceId = Guid.NewGuid();
        var apiResponse = new ApiResponse<bool>(false, "Space not found", false);
        
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<ToggleFavoriteCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        // Act
        var result = await _controller.ToggleFavorite(parkingSpaceId, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be(apiResponse);
    }
}
