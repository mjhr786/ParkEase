using System;
using System.Collections.Generic;
using FluentAssertions;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.Mappings;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Enums;
using Xunit;

namespace ParkingApp.UnitTests.Mappings;

public class MappingExtensionsTests
{
    [Fact]
    public void UserMapping_ShouldMapCorrectly()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            FirstName = "First",
            LastName = "Last",
            Role = UserRole.Member,
            PhoneNumber = "123"
        };

        var dto = user.ToDto();

        dto.Id.Should().Be(user.Id);
        dto.Email.Should().Be(user.Email);
        dto.FirstName.Should().Be(user.FirstName);
        dto.LastName.Should().Be(user.LastName);
        dto.Role.Should().Be(user.Role);
        dto.PhoneNumber.Should().Be(user.PhoneNumber);
    }

    [Fact]
    public void ParkingSpaceMapping_ShouldMapCorrectly()
    {
        var parking = new ParkingSpace
        {
            Id = Guid.NewGuid(),
            OwnerId = Guid.NewGuid(),
            Title = "Title",
            Amenities = "Wi-Fi,CCTV",
            AllowedVehicleTypes = "Car,Motorcycle"
        };

        var dto = parking.ToDto();

        dto.Id.Should().Be(parking.Id);
        dto.Title.Should().Be(parking.Title);
        dto.Amenities.Should().Contain("Wi-Fi");
        dto.Amenities.Should().Contain("CCTV");
        dto.AllowedVehicleTypes.Should().Contain(VehicleType.Car);
        dto.AllowedVehicleTypes.Should().Contain(VehicleType.Motorcycle);
    }
    
    [Fact]
    public void ParkingSpaceMapping_WithReservations_ShouldMapCorrectly()
    {
        var parking = new ParkingSpace { Id = Guid.NewGuid(), Title = "A", ParkingType = ParkingType.Open, Latitude = 1, Longitude = -1 };
        var bookings = new List<Booking>
        {
            new Booking { Status = BookingStatus.Confirmed, StartDateTime = DateTime.UtcNow.AddDays(1), EndDateTime = DateTime.UtcNow.AddDays(2) },
            new Booking { Status = BookingStatus.Cancelled, StartDateTime = DateTime.UtcNow.AddDays(3), EndDateTime = DateTime.UtcNow.AddDays(4) }
        };

        var dto = parking.ToDtoWithReservations(bookings);

        dto.Id.Should().Be(parking.Id);
        dto.ActiveReservations.Should().NotBeNull();
        dto.ActiveReservations.Count.Should().Be(1);
    }

    [Fact]
    public void CreateParkingSpaceDtoMapping_ShouldMapToEntityCorrectly()
    {
        var dto = new CreateParkingSpaceDto("T", "D", "A", "C", "S", "Co", "P", 1.1, 2.2, ParkingType.Open, 1, 1, 1, 1, 1, null, null, false, new List<string>{"Wi-Fi"}, new List<VehicleType>{VehicleType.Car}, null, null);
        var ownerId = Guid.NewGuid();

        var entity = dto.ToEntity(ownerId);

        entity.OwnerId.Should().Be(ownerId);
        entity.Title.Should().Be(dto.Title);
        entity.Latitude.Should().Be(1.1);
        entity.Longitude.Should().Be(2.2);
        entity.Amenities.Should().Be("Wi-Fi");
        entity.AllowedVehicleTypes.Should().Be("Car");
        entity.Location.Coordinate.X.Should().Be(2.2);
        entity.Location.Coordinate.Y.Should().Be(1.1);
    }

    [Fact]
    public void BookingMapping_ShouldMapCorrectly()
    {
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ParkingSpaceId = Guid.NewGuid(),
            Status = BookingStatus.Pending
        };

        var dto = booking.ToDto();

        dto.Id.Should().Be(booking.Id);
        dto.UserId.Should().Be(booking.UserId);
        dto.ParkingSpaceId.Should().Be(booking.ParkingSpaceId);
        dto.Status.Should().Be(booking.Status);
    }

    [Fact]
    public void PaymentMapping_ShouldMapCorrectly()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Amount = 100,
            Status = PaymentStatus.Completed
        };

        var dto = payment.ToDto();

        dto.Id.Should().Be(payment.Id);
        dto.Amount.Should().Be(payment.Amount);
        dto.Status.Should().Be(payment.Status);
    }

    [Fact]
    public void ReviewMapping_ShouldMapCorrectly()
    {
        var review = new Review
        {
            Id = Guid.NewGuid(),
            Rating = 5,
            Title = "Great"
        };

        var dto = review.ToDto();

        dto.Id.Should().Be(review.Id);
        dto.Rating.Should().Be(review.Rating);
        dto.Title.Should().Be(review.Title);
    }

    [Fact]
    public void ChatMessageMapping_ShouldMapCorrectly()
    {
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            Content = "Hello"
        };

        var dto = message.ToDto();

        dto.Id.Should().Be(message.Id);
        dto.Content.Should().Be(message.Content);
    }

    [Fact]
    public void ConversationMapping_ShouldMapCorrectly()
    {
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            VendorId = Guid.NewGuid(),
            User = new User { Id = Guid.NewGuid(), FirstName = "User" },
            Vendor = new User { Id = Guid.NewGuid(), FirstName = "Vendor" }
        };

        var dtoAsUser = conversation.ToDto(conversation.UserId, 1);
        var dtoAsVendor = conversation.ToDto(conversation.VendorId, 0);

        dtoAsUser.Id.Should().Be(conversation.Id);
        dtoAsUser.UnreadCount.Should().Be(1);
        dtoAsVendor.UnreadCount.Should().Be(0);
    }
}
