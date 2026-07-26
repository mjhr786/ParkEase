using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;

using ParkingApp.Corporate.Domain;
using ParkingApp.Corporate.Domain.Interfaces; // Corporate UoW ports (historical namespace in Corporate.Domain)
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Persistence;
using ParkingApp.Messaging.Domain.Interfaces;
using ParkingApp.Messaging.Infrastructure.Repositories;
using ParkingApp.Corporate.Infrastructure.Repositories;
using ParkingApp.Identity.Infrastructure.Repositories;
using ParkingApp.Marketplace.Infrastructure.Repositories;

namespace ParkingApp.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork, ICorporateUnitOfWork
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
    private ILprAccessAttemptRepository? _lprAccessAttempts;
    private ILprCameraKeyRepository? _lprCameraKeys;
    private ILprPlateRuleRepository? _lprPlateRules;
    private IEventParkingPackageRepository? _eventParkingPackages;
    private IEvChargingSessionRepository? _evChargingSessions;
    private IParkingAncillaryServiceRepository? _parkingAncillaryServices;
    private INotificationRepository? _notifications;
    private IVehicleRepository? _vehicles;
    private IDeviceTokenRepository? _deviceTokens;
    private ICompanyRepository? _companies;
    private ICorporateBookingRepository? _corporateBookings;
    private IEmployeeInvitationRepository? _employeeInvitations;
    private ICorporateInvoiceRepository? _invoices;

    private readonly List<Guid> _pendingOutboxMessageIds = new();

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
    public ILprAccessAttemptRepository LprAccessAttempts => _lprAccessAttempts ??= new LprAccessAttemptRepository(_context);
    public ILprCameraKeyRepository LprCameraKeys => _lprCameraKeys ??= new LprCameraKeyRepository(_context);
    public ILprPlateRuleRepository LprPlateRules => _lprPlateRules ??= new LprPlateRuleRepository(_context);
    public IEventParkingPackageRepository EventParkingPackages =>
        _eventParkingPackages ??= new EventParkingPackageRepository(_context);
    public IEvChargingSessionRepository EvChargingSessions =>
        _evChargingSessions ??= new EvChargingSessionRepository(_context);
    public IParkingAncillaryServiceRepository ParkingAncillaryServices =>
        _parkingAncillaryServices ??= new ParkingAncillaryServiceRepository(_context);
    public INotificationRepository Notifications => _notifications ??= new NotificationRepository(_context);
    public IVehicleRepository Vehicles => _vehicles ??= new VehicleRepository(_context);
    public IDeviceTokenRepository DeviceTokens => _deviceTokens ??= new DeviceTokenRepository(_context);
    public ICompanyRepository Companies => _companies ??= new CompanyRepository(_context);
    public ICorporateBookingRepository CorporateBookings => _corporateBookings ??= new CorporateBookingRepository(_context);
    public IEmployeeInvitationRepository EmployeeInvitations => _employeeInvitations ??= new EmployeeInvitationRepository(_context);
    public ICorporateInvoiceRepository Invoices => _invoices ??= new CorporateInvoiceRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1) Collect domain events BEFORE save so outbox rows share this transaction.
        // BuildingBlocks.BaseEntity covers Domain.Shared.BaseEntity and module domains.
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
                if (_transaction != null)
                {
                    // Delay processing until transaction commits to avoid Read-Uncommitted race conditions
                    _pendingOutboxMessageIds.AddRange(enqueuedIds);
                }
                else
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

            if (_pendingOutboxMessageIds.Count > 0)
            {
                var idsToProcess = _pendingOutboxMessageIds.ToList();
                _pendingOutboxMessageIds.Clear();
                try
                {
                    await _outboxProcessor.ProcessByIdsAsync(idsToProcess, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Post-commit outbox processing failed; background service will retry");
                }
            }
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
