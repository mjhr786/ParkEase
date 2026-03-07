using System;
using System.Collections.Generic;
using FluentAssertions;
using ParkingApp.BuildingBlocks.Exceptions;
using Xunit;

namespace ParkingApp.UnitTests.BuildingBlocks;

public class DomainExceptionsTests
{
    [Fact]
    public void NotFoundException_ShouldSetProperties()
    {
        var ex = new NotFoundException("User", 1);
        ex.Message.Should().Be("User not found (Id: 1)");
        ex.ResourceType.Should().Be("User");
        ex.ResourceId.Should().Be(1);
        ex.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void NotFoundException_For_ShouldSetType()
    {
        var ex = NotFoundException.For<DomainExceptionsTests>(5);
        ex.ResourceType.Should().Be(nameof(DomainExceptionsTests));
        ex.ResourceId.Should().Be(5);
    }

    [Fact]
    public void ValidationException_Message_ShouldSetProperties()
    {
        var ex = new ValidationException("Error");
        ex.Message.Should().Be("Error");
        ex.Errors.Should().BeEmpty();
        ex.ErrorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public void ValidationException_Dictionary_ShouldSetProperties()
    {
        var dict = new Dictionary<string, string[]> { { "Field", new[] { "Err" } } };
        var ex = new ValidationException(dict);
        ex.Errors.Should().BeEquivalentTo(dict);
    }

    [Fact]
    public void ValidationException_FieldAndError_ShouldSetProperties()
    {
        var ex = new ValidationException("Field", "Error message");
        ex.Message.Should().Be("Validation failed for Field: Error message");
        ex.Errors.Should().ContainKey("Field").WhoseValue.Should().Contain("Error message");
    }

    [Fact]
    public void UnauthorizedException_ShouldSetProperties()
    {
        var ex = new UnauthorizedException();
        ex.Message.Should().NotBeNullOrEmpty();
        ex.ErrorCode.Should().Be("UNAUTHORIZED");
        
        var custom = new UnauthorizedException("custom");
        custom.Message.Should().Be("custom");
    }

    [Fact]
    public void ForbiddenException_ShouldSetProperties()
    {
        var ex = new ForbiddenException();
        ex.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void BusinessRuleException_ShouldSetProperties()
    {
        var ex = new BusinessRuleException("Rule1", "Msg");
        ex.RuleName.Should().Be("Rule1");
        ex.Message.Should().Be("Msg");
        ex.ErrorCode.Should().Be("BUSINESS_RULE_VIOLATION");
    }

    [Fact]
    public void ConflictException_ShouldSetProperties()
    {
        var ex = new ConflictException("Msg");
        ex.Message.Should().Be("Msg");
        ex.ErrorCode.Should().Be("CONFLICT");
    }

    [Fact]
    public void ExternalServiceException_ShouldSetProperties()
    {
        var inner = new Exception("Inner");
        var ex = new ExternalServiceException("Service", "Msg", inner);
        ex.ServiceName.Should().Be("Service");
        ex.Message.Should().Be("Service: Msg");
        ex.InnerException.Should().Be(inner);
        ex.ErrorCode.Should().Be("EXTERNAL_SERVICE_ERROR");
    }
}
