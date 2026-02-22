using Microsoft.EntityFrameworkCore.Storage;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private IDbContextTransaction? _transaction;
    
    private IUserRepository? _users;
    private IParkingSpaceRepository? _parkingSpaces;
    private IBookingRepository? _bookings;
    private IPaymentRepository? _payments;
    private IReviewRepository? _reviews;
    private IConversationRepository? _conversations;
    private IChatMessageRepository? _chatMessages;

    public UnitOfWork(ApplicationDbContext context, IDomainEventDispatcher eventDispatcher)
    {
        _context = context;
        _eventDispatcher = eventDispatcher;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IParkingSpaceRepository ParkingSpaces => _parkingSpaces ??= new ParkingSpaceRepository(_context);
    public IBookingRepository Bookings => _bookings ??= new BookingRepository(_context);
    public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
    public IReviewRepository Reviews => _reviews ??= new ReviewRepository(_context);
    public IConversationRepository Conversations => _conversations ??= new ConversationRepository(_context);
    public IChatMessageRepository ChatMessages => _chatMessages ??= new ChatMessageRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Persist changes first
        var result = await _context.SaveChangesAsync(cancellationToken);

        // Collect and dispatch domain events from all tracked entities
        var entitiesWithEvents = _context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events before dispatching to prevent re-entrancy issues
        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        if (domainEvents.Any())
        {
            await _eventDispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
        }

        return result;
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

