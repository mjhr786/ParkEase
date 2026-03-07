using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ParkingApp.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Repositories;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Repositories;

public class GenericRepositoryTests
{
    private readonly ApplicationDbContext _context;
    private readonly Repository<User> _repository;

    public GenericRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _repository = new Repository<User>(_context);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntity()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "t@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.GetByIdAsync(user.Id);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAll()
    {
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "t1@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "t2@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        await _context.SaveChangesAsync();

        var result = await _repository.GetAllAsync();
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindAsync_ReturnsMatching()
    {
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "find@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        await _context.SaveChangesAsync();

        var result = await _repository.FindAsync(u => u.Email == "find@t.com");
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsFirst()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "first@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await _repository.FirstOrDefaultAsync(u => u.Email == "first@t.com");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddAsync_Adds()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "add@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();
        _context.Users.Count().Should().Be(1);
    }

    [Fact]
    public async Task AnyAsync_ReturnsTrue()
    {
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "any@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        await _context.SaveChangesAsync();
        var result = await _repository.AnyAsync(u => u.Email == "any@t.com");
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CountAsync_ReturnsCount()
    {
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "c1@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        _context.Users.Add(new User { Id = Guid.NewGuid(), Email = "c2@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" });
        await _context.SaveChangesAsync();
        var result = await _repository.CountAsync(u => true);
        result.Should().Be(2);
    }

    [Fact]
    public async Task Remove_SetsIsDeleted()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "del@t.com", PasswordHash = "h", FirstName = "F", LastName = "L", PhoneNumber = "P" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _repository.Remove(user);
        await _context.SaveChangesAsync();
        var result = await _context.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        result.IsDeleted.Should().BeTrue();
    }
}
