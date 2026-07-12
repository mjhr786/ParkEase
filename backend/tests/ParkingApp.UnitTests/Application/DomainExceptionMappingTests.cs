using FluentAssertions;
using ParkingApp.Application.Common;
using ParkingApp.BuildingBlocks.Exceptions;
using Xunit;

namespace ParkingApp.UnitTests.Application;

public class DomainExceptionMappingTests
{
    [Fact]
    public void ToFailureResponse_MapsBusinessRuleException()
    {
        var ex = new BusinessRuleException("Booking.CheckIn", "Cannot check in booking in Pending status");

        var response = DomainExceptionMapping.ToFailureResponse<string>(ex);

        response.Success.Should().BeFalse();
        response.Message.Should().Be(ex.Message);
        response.Data.Should().BeNull();
        response.Errors.Should().ContainSingle(ex.Message);
    }

    [Fact]
    public void ToFailureResponse_MapsValidationExceptionErrors()
    {
        var ex = new ValidationException("discountAmount", "Invalid discount amount");

        var response = DomainExceptionMapping.ToFailureResponse<object>(ex);

        response.Success.Should().BeFalse();
        response.Errors.Should().Contain("Invalid discount amount");
    }
}
