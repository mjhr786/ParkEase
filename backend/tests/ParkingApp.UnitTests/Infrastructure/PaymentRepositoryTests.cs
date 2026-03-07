using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class PaymentRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly PaymentRepository _repository;

    public PaymentRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new PaymentRepository(_context);
    }

    private (User, Booking) CreateBaseEntities()
    {
        var user = new User { Id = Guid.NewGuid(), Email = $"u{Guid.NewGuid()}@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        var space = new ParkingSpace { Id = Guid.NewGuid(), Title = "S", Description = "D", Address = "A", City = "C", State = "S", Country = "Cu", PostalCode = "P", OwnerId = Guid.NewGuid() };
        var booking = new Booking { 
            Id = Guid.NewGuid(), UserId = user.Id, ParkingSpaceId = space.Id, 
            StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddHours(1),
            BaseAmount = 10, TotalAmount = 10
        };
        _context.Users.Add(user);
        _context.ParkingSpaces.Add(space);
        _context.Bookings.Add(booking);
        return (user, booking);
    }

    [Fact]
    public async Task GetByBookingIdAsync_ReturnsPayment()
    {
        // Arrange
        var (user, booking) = CreateBaseEntities();
        var payment = new Payment { Id = Guid.NewGuid(), BookingId = booking.Id, UserId = user.Id, Amount = 10, Currency = "USD", Status = ParkingApp.Domain.Enums.PaymentStatus.Completed };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByBookingIdAsync(booking.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }

    [Fact]
    public async Task GetByTransactionIdAsync_ReturnsPayment()
    {
        // Arrange
        var (user, booking) = CreateBaseEntities();
        var payment = new Payment { Id = Guid.NewGuid(), BookingId = booking.Id, UserId = user.Id, Amount = 10, Currency = "USD", TransactionId = "txn_123" };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByTransactionIdAsync("txn_123");

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
    }
}
