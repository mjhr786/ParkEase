using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.Configuration;
using ParkingApp.Infrastructure.Services;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using ParkingApp.BuildingBlocks.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using ParkingApp.API.Middleware;
using System.IO;
using System.Text;

namespace ParkingApp.UnitTests;

public class AdvancedInfrastructureTests
{
    [Fact]
    public void JwtTokenService_GenerateAccessToken_ShouldReturnValidToken()
    {
        // Arrange
        var mockConfig = new Mock<IConfiguration>();
        mockConfig.Setup(c => c["Jwt:SecretKey"]).Returns("SuperSecretKey12345678901234567890");
        mockConfig.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        mockConfig.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        mockConfig.Setup(c => c["Jwt:AccessTokenExpirationMinutes"]).Returns("60");

        var service = new JwtTokenService(mockConfig.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", Role = UserRole.Member, FirstName = "John", LastName = "Doe" };

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Split('.').Should().HaveCount(3); // Header.Payload.Signature
    }

    [Fact]
    public void Result_Generic_ShouldHandleSuccessAndFailure()
    {
        var success = Result<string>.Success("data");
        success.IsSuccess.Should().BeTrue();
        success.Value.Should().Be("data");

        var failure = Result<string>.Failure("error");
        failure.IsSuccess.Should().BeFalse();
        failure.Error.Should().Be("error");
    }

    [Fact]
    public async Task ImageResizingMiddleware_WithoutResizeQuery_ShouldPassThrough()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/uploads/test.jpg";
        
        var mockLogger = new Mock<ILogger<ImageResizingMiddleware>>();
        var mockEnv = new Mock<IWebHostEnvironment>();
        
        bool nextCalled = false;
        RequestDelegate next = (innerContext) => { 
            nextCalled = true; 
            return Task.CompletedTask; 
        };

        var middleware = new ImageResizingMiddleware(next, mockLogger.Object, mockEnv.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void PagedResult_ShouldCalculateMetadataCorrectly()
    {
        var items = new List<int> { 1, 2, 3 };
        var result = new PagedResult<int>(items, 10, 1, 3);

        result.TotalCount.Should().Be(10);
        result.TotalPages.Should().Be(4); // Ceiling(10/3)
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeFalse();
    }
}
