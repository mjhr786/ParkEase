using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Corporate;
using ParkingApp.Domain.Events;
using ParkingApp.Domain.Identity;
using ParkingApp.Domain.Interfaces;
using ParkingApp.Domain.Marketplace;
using ParkingApp.Domain.Messaging;
using ParkingApp.Domain.Shared;
using ParkingApp.Infrastructure.Data;

namespace ParkingApp.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IOutboxProcessor _outboxProcessor;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction? _transaction;

    private IUserRepository? _users;
    private IParkingSpaceRepository? _parkingSpaces;
    private IBookingRepository? _bookings;
    private IParkingPassRepository? _parkingPasses;
    private IPaymentRepository? _payments;
    private IReviewRepository? _reviews;
    private IConversationRepository? _conversations;
    private IChatMessageRepository? _chatMessages;
    private IFavoriteRepository? _favorites;
    private INotificationRepository? _notifications;
    private IVehicleRepository? _vehicles;
    private IDeviceTokenRepository? _deviceTokens;
    private ICompanyRepository? _companies;
    private ICorporateBookingRepository? _corporateBookings;
    private IEmployeeInvitationRepository? _employeeInvitations;
    private ICorporateInvoiceRepository? _invoices;

    public UnitOfWork(
        ApplicationDbContext context,
        IOutboxWriter outboxWriter,
        IOutboxProcessor outboxProcessor,
        ILogger<UnitOfWork> logger)
    {
        _context = context;
        _outboxWriter = outboxWriter;
        _outboxProcessor = outboxProcessor;
        _logger = logger;
    }

    public IUserRepository Users => _users ??= new UserRepository(_context);
    public IParkingSpaceRepository ParkingSpaces => _parkingSpaces ??= new ParkingSpaceRepository(_context);
    public IBookingRepository Bookings => _bookings ??= new BookingRepository(_context);
    public IParkingPassRepository ParkingPasses => _parkingPasses ??= new ParkingPassRepository(_context);
    public IPaymentRepository Payments => _payments ??= new PaymentRepository(_context);
    public IReviewRepository Reviews => _reviews ??= new ReviewRepository(_context);
    public IConversationRepository Conversations => _conversations ??= new ConversationRepository(_context);
    public IChatMessageRepository ChatMessages => _chatMessages ??= new ChatMessageRepository(_context);
    public IFavoriteRepository Favorites => _favorites ??= new FavoriteRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IVehicleRepository Vehicles => _vehicles ??= new VehicleRepository(_context);
    public IDeviceTokenRepository DeviceTokens => _deviceTokens ??= new DeviceTokenRepository(_context);
    public ICompanyRepository Companies => _companies ??= new CompanyRepository(_context);
    public ICorporateBookingRepository CorporateBookings => _corporateBookings ??= new CorporateBookingRepository(_context);
    public IEmployeeInvitationRepository EmployeeInvitations => _employeeInvitations ??= new EmployeeInvitationRepository(_context);
    public ICorporateInvoiceRepository Invoices => _invoices ??= new CorporateInvoiceRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1) Collect domain events BEFORE save so outbox rows share this transaction
        var entitiesWithEvents = _context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
            entity.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
            _outboxWriter.Enqueue(domainEvent);

        // 2) Persist aggregates + outbox together
        var result = await _context.SaveChangesAsync(cancellationToken);

        // 3) Fast-path: process ONLY messages staged by this SaveChanges (not a global batch of 50).
        //    Background OutboxBackgroundService still drains Pending/Failed with backoff.
        if (domainEvents.Count > 0)
        {
            var enqueuedIds = _outboxWriter.TakeEnqueuedMessageIds();
            if (enqueuedIds.Count > 0)
            {
                try
                {
                    await _outboxProcessor.ProcessByIdsAsync(enqueuedIds, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Immediate outbox processing failed; background service will retry");
                }
            }
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
