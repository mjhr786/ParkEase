using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using ParkingApp.Application.DTOs;
using ParkingApp.Identity.Application.DTOs;
using ParkingApp.Identity.Domain.Enums;
using Xunit;

namespace ParkingApp.UnitTests.Identity;

public class TokenDtoAndApiResponseTests
{
    [Fact]
    public void TokenDto_PropertyInit_ExposesChannelAndOptionalCompanyFields()
    {
        var user = new UserDto(
            Guid.NewGuid(),
            "a@b.com",
            "A",
            "B",
            "1",
            UserRole.User,
            true,
            false,
            DateTime.UtcNow);
        var companyId = Guid.NewGuid();

        var dto = new TokenDto
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = user,
            Channel = "Corporate",
            CompanyId = companyId,
            CompanyRole = "Admin",
            IsBootstrap = false
        };

        dto.AccessToken.Should().Be("at");
        dto.Channel.Should().Be("Corporate");
        dto.CompanyId.Should().Be(companyId);
        dto.CompanyRole.Should().Be("Admin");
        dto.IsBootstrap.Should().BeFalse();
    }

    [Fact]
    public void TokenDto_Bootstrap_SerializesChannelWithoutCompany()
    {
        var user = new UserDto(
            Guid.NewGuid(),
            "a@b.com",
            "A",
            "B",
            "1",
            UserRole.User,
            true,
            false,
            DateTime.UtcNow);

        var dto = new TokenDto
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = user,
            Channel = "Corporate",
            IsBootstrap = true
        };

        var json = JsonSerializer.Serialize(dto);
        json.Should().Contain("Corporate");
        json.Should().Contain("IsBootstrap");
        dto.CompanyId.Should().BeNull();
        dto.IsBootstrap.Should().BeTrue();
    }

    [Fact]
    public void ApiResponse_Code_Optional_PositionalFourArgsRemainValid()
    {
        var withoutCode = new ApiResponse<string>(true, "ok", "data", null);
        withoutCode.Code.Should().BeNull();
        withoutCode.Success.Should().BeTrue();

        var withCode = new ApiResponse<string>(false, "denied", null, new List<string> { "channel_forbidden" }, "channel_forbidden");
        withCode.Code.Should().Be("channel_forbidden");
        withCode.Errors.Should().Contain("channel_forbidden");

        var json = JsonSerializer.Serialize(withCode);
        json.Should().Contain("channel_forbidden");
    }

    [Fact]
    public void ApiResponse_ThreeArgPositional_StillCompilesAndDefaultsCode()
    {
        var response = new ApiResponse<int>(true, "msg", 42);
        response.Data.Should().Be(42);
        response.Errors.Should().BeNull();
        response.Code.Should().BeNull();
    }
}
