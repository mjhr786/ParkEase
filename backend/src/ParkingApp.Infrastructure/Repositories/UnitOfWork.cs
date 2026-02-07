using Microsoft.EntityFrameworkCore.Storage;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private IDbContextTransaction? _transaction;
    
    private IUserRepository? _users;
    private IParkingSpaceRepository? _parkingSpaces;
    private IBookingRepository? _bookings;
    private IPaymentRepository? _payments;
    private IReviewRepository? _reviews;

    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IParkingSpaceRepository ParkingSpaces => _parkingSpaces ??= new ParkingSpaceRepository(_context);
    public IBookingRepository Bookings => _bookings ??= new BookingRepository(_context);
    public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
    public IReviewRepository Reviews => _reviews ??= new ReviewRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
