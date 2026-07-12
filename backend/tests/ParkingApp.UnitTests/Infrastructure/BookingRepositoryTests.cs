using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Shared;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class BookingRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly BookingRepository _repository;

    public BookingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new BookingRepository(_context);
    }

    private (User, ParkingSpace) CreateBaseEntities()
    {
        var owner = new User { Id = Guid.NewGuid(), Email = $"owner{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var user = new User { Id = Guid.NewGuid(), Email = $"user{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "S", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = owner.Id };
        
        _context.Users.AddRange(owner, user);
        _context.ParkingSpaces.Add(space);
        
        return (user, space);
    }

    [Fact]
    public async Task GetByIdWithDetailsAsync_IncludesRelations()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        var booking = new Booking { 
            Id = Guid.NewGuid(), UserId = user.Id, ParkingSpaceId = space.Id, 
            StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddHours(1),
            BaseAmount = 10, TotalAmount = 10, Status = BookingStatus.Confirmed
        };
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdWithDetailsAsync(booking.Id);

        // Assert
        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.ParkingSpace.Should().NotBeNull();
        result.ParkingSpace.Owner.Should().NotBeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsUserBookings()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        _context.Bookings.Add(new Booking { Id = Guid.NewGuid(), UserId = user.Id, ParkingSpaceId = space.Id, StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddHours(1), BaseAmount = 10, TotalAmount = 10 });
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByUserIdAsync(user.Id);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task HasOverlappingBookingAsync_DetectsOverlap()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        var start = DateTime.UtcNow.AddDays(1);
        var end = DateTime.UtcNow.AddDays(1).AddHours(2);
        _context.Bookings.Add(new Booking { 
            Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, 
            StartDateTime = start, EndDateTime = end, Status = BookingStatus.Confirmed,
            BaseAmount = 10, TotalAmount = 10
        });
        await _context.SaveChangesAsync();

        // Act & Assert
        var overlap = await _repository.HasOverlappingBookingAsync(space.Id, start.AddHours(-1), start.AddHours(1));
        overlap.Should().BeTrue();
    }

    [Fact]
    public async Task HasBlockingBookingsForSpaceAsync_TrueWhenConfirmedAndNotEnded()
    {
        var (user, space) = CreateBaseEntities();
        var now = DateTime.UtcNow;
        _context.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ParkingSpaceId = space.Id,
            StartDateTime = now.AddHours(-1),
            EndDateTime = now.AddHours(2),
            Status = BookingStatus.Confirmed,
            BaseAmount = 10,
            TotalAmount = 10
        });
        await _context.SaveChangesAsync();

        var blocking = await _repository.HasBlockingBookingsForSpaceAsync(space.Id, now);
        blocking.Should().BeTrue();
    }

    [Fact]
    public async Task HasBlockingBookingsForSpaceAsync_FalseWhenCompleted()
    {
        var (user, space) = CreateBaseEntities();
        var now = DateTime.UtcNow;
        _context.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ParkingSpaceId = space.Id,
            StartDateTime = now.AddHours(-3),
            EndDateTime = now.AddHours(-1),
            Status = BookingStatus.Completed,
            BaseAmount = 10,
            TotalAmount = 10
        });
        await _context.SaveChangesAsync();

        var blocking = await _repository.HasBlockingBookingsForSpaceAsync(space.Id, now);
        blocking.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveBookingsCountAsync_ReturnsCount()
    {
        // Arrange
        var (user, space) = CreateBaseEntities();
        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddHours(1);
        _context.Bookings.Add(new Booking { Id = Guid.NewGuid(), ParkingSpaceId = space.Id, UserId = user.Id, Status = BookingStatus.InProgress, StartDateTime = start, EndDateTime = end, BaseAmount = 10, TotalAmount = 10 });
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetActiveBookingsCountAsync(space.Id, start, end);

        // Assert
        count.Should().Be(1);
    }
}
