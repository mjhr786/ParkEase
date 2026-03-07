using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ParkingApp.API.Middleware;
using ParkingApp.Application.DTOs;
using Xunit;

namespace ParkingApp.UnitTests.API.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlingMiddleware>> _loggerMock;
    
    public ExceptionHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ExceptionHandlingMiddleware>>();
    }

    [Theory]
    [InlineData(typeof(ArgumentException), HttpStatusCode.BadRequest, "Test argument error")]
    [InlineData(typeof(UnauthorizedAccessException), HttpStatusCode.Unauthorized, "Unauthorized access")]
    [InlineData(typeof(KeyNotFoundException), HttpStatusCode.NotFound, "Resource not found")]
    [InlineData(typeof(InvalidOperationException), HttpStatusCode.BadRequest, "Test invalid op error")]
    [InlineData(typeof(Exception), HttpStatusCode.InternalServerError, "An error occurred. Please try again later.")]
    public async Task InvokeAsync_WhenExceptionThrown_ReturnsCorrectStatusCodeAndResponse(Type exceptionType, HttpStatusCode expectedStatus, string expectedMessage)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, expectedMessage)!;
        
        RequestDelegate next = (HttpContext hc) => Task.FromException(exception);
        var middleware = new ExceptionHandlingMiddleware(next, _loggerMock.Object);
        
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)expectedStatus);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(context.Response.Body);
        var jsonResponse = await reader.ReadToEndAsync();
        
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var apiResponse = JsonSerializer.Deserialize<ApiResponse<object>>(jsonResponse, options);

        apiResponse.Should().NotBeNull();
        apiResponse!.Success.Should().BeFalse();
        apiResponse.Message.Should().Be(expectedMessage);
        apiResponse.Errors.Should().Contain(expectedMessage);
    }
}
