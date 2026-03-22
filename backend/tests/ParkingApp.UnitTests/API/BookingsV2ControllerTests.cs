using Moq;
using FluentAssertions;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using ParkingApp.API.Controllers;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Domain.Enums;

namespace ParkingApp.UnitTests.API;

public class BookingsV2ControllerTests
{
    private readonly Mock<IDispatcher> _mockDispatcher;
    private readonly BookingsV2Controller _controller;
    private readonly Guid _userId;

    public BookingsV2ControllerTests()
    {
        _mockDispatcher = new Mock<IDispatcher>();
        _controller = new BookingsV2Controller(_mockDispatcher.Object);
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
    public async Task GetById_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var resultDto = new ApiResponse<BookingDto>(true, null, CreateDummyBookingDto(bookingId));
        
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetBookingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.GetById(bookingId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(resultDto);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var resultDto = new ApiResponse<BookingDto>(false, "Booking not found", null);

        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetBookingByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.GetById(bookingId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be(resultDto);
    }

    [Fact]
    public async Task Update_WhenSuccessful_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var dto = new UpdateBookingDto(null, null, null, "NEW-123", null);
        var resultDto = new ApiResponse<BookingDto>(true, "Updated", CreateDummyBookingDto(bookingId));

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<UpdateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.Update(bookingId, dto, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(resultDto);
    }

    [Fact]
    public async Task Update_WhenUnauthorized_ShouldReturnForbid()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var dto = new UpdateBookingDto(null, null, null, "NEW-123", null);
        var resultDto = new ApiResponse<BookingDto>(false, "Unauthorized", null);

        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<UpdateBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.Update(bookingId, dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetPendingRequestsCount_ShouldReturnOk()
    {
        // Arrange
        var resultDto = new ApiResponse<int>(true, null, 5);
        _mockDispatcher.Setup(d => d.QueryAsync(It.IsAny<GetPendingRequestsCountQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.GetPendingRequestsCount(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(resultDto);
    }

    [Fact]
    public async Task Cancel_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var dto = new CancelBookingDto("Reason");
        var resultDto = new ApiResponse<BookingDto>(true, "Cancelled", CreateDummyBookingDto(bookingId));
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<CancelBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.Cancel(bookingId, dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Approve_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var resultDto = new ApiResponse<BookingDto>(true, "Approved", CreateDummyBookingDto(bookingId));
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<ApproveBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.Approve(bookingId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Reject_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var dto = new global::ParkingApp.API.Controllers.RejectBookingDto("Reason");
        var resultDto = new ApiResponse<BookingDto>(true, "Rejected", CreateDummyBookingDto(bookingId));
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<RejectBookingCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.Reject(bookingId, dto, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckIn_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var resultDto = new ApiResponse<BookingDto>(true, "CheckedIn", CreateDummyBookingDto(bookingId));
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<CheckInCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.CheckIn(bookingId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckOut_ShouldReturnOk()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var resultDto = new ApiResponse<BookingDto>(true, "CheckedOut", CreateDummyBookingDto(bookingId));
        _mockDispatcher.Setup(d => d.SendAsync(It.IsAny<CheckOutCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultDto);

        // Act
        var result = await _controller.CheckOut(bookingId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    private BookingDto CreateDummyBookingDto(Guid id)
    {
        return new BookingDto { Id = id };
    }
}

