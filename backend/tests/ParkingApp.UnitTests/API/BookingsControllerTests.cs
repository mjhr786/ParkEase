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
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Common;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class BookingsControllerTests
{
    private readonly Mock<IBookingService> _bookingServiceMock;
    private readonly Mock<IValidator<CreateBookingDto>> _createValidatorMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        _bookingServiceMock = new Mock<IBookingService>();
        _createValidatorMock = new Mock<IValidator<CreateBookingDto>>();
        _dispatcherMock = new Mock<IDispatcher>();

        _controller = new BookingsController(
            _bookingServiceMock.Object,
            _createValidatorMock.Object);
    }

    private void SetupControllerUser(ControllerBase controller, Guid userId, string role = "User")
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
    public async Task GetById_ReturnsOkObject()
    {
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _bookingServiceMock.Setup(s => s.GetByIdAsync(bookingId, userId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingDto>(true, "Success", new BookingDto(), null)));

        var result = await _controller.GetById(bookingId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyBookings_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _bookingServiceMock.Setup(s => s.GetByUserAsync(userId, It.IsAny<BookingFilterDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingListResultDto>(true, "Success", new BookingListResultDto(new List<BookingDto>(), 0, 1, 10, 1), null)));

        var result = await _controller.GetMyBookings(null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_ValidDto_ReturnsCreated()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new CreateBookingDto(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(2), ParkingApp.Domain.Enums.PricingType.Hourly, ParkingApp.Domain.Enums.VehicleType.Car, null, null, null, null, null);
        var bookingId = Guid.NewGuid();

        _createValidatorMock.Setup(v => v.ValidateAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _bookingServiceMock.Setup(s => s.CreateAsync(userId, dto, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingDto>(true, "Success", new BookingDto() { Id = bookingId }, null)));

        var result = await _controller.Create(dto, CancellationToken.None);

        var createdResult = result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Contain(bookingId.ToString());
    }

    [Fact]
    public async Task Cancel_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);
        var dto = new CancelBookingDto("Test");

        _bookingServiceMock.Setup(s => s.CancelAsync(bookingId, userId, dto, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingDto>(true, "Success", new BookingDto(), null)));

        var result = await _controller.Cancel(bookingId, dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetVendorBookings_VendorRole_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        SetupControllerUser(_controller, userId, "Vendor");

        _bookingServiceMock.Setup(s => s.GetVendorBookingsAsync(userId, It.IsAny<BookingFilterDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingListResultDto>(true, "Success", new BookingListResultDto(new List<BookingDto>(), 0, 1, 10, 1), null)));

        var result = await _controller.GetVendorBookings(null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CalculatePrice_ReturnsOk()
    {
        var dto = new PriceCalculationDto(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(2), ParkingApp.Domain.Enums.PricingType.Hourly, null);
        _bookingServiceMock.Setup(s => s.CalculatePriceAsync(dto, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<PriceBreakdownDto>(true, "Success", new PriceBreakdownDto(10m, 0m, 1m, 11m, 10m, "1h", 1, "h"), null)));

        var result = await _controller.CalculatePrice(dto, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckIn_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _bookingServiceMock.Setup(s => s.CheckInAsync(bookingId, userId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingDto>(true, "Success", new BookingDto(), null)));

        var result = await _controller.CheckIn(bookingId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckOut_ReturnsOk()
    {
        var userId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        SetupControllerUser(_controller, userId);

        _bookingServiceMock.Setup(s => s.CheckOutAsync(bookingId, userId, It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(new ApiResponse<BookingDto>(true, "Success", new BookingDto(), null)));

        var result = await _controller.CheckOut(bookingId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }
}
