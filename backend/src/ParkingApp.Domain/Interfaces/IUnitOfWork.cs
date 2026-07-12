namespace ParkingApp.Domain.Interfaces;

/// <summary>
/// Persist + transaction boundary. Implemented once by Infrastructure <c>UnitOfWork</c>;
/// domain events dispatch after each successful <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IUnitOfWorkTransaction
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Marketplace write-side aggregate repositories.
/// Prefer this over full <see cref="IUnitOfWork"/> in marketplace handlers.
/// </summary>
public interface IMarketplaceUnitOfWork : IUnitOfWorkTransaction
{
    IParkingSpaceRepository ParkingSpaces { get; }
    IBookingRepository Bookings { get; }
    IParkingPassRepository ParkingPasses { get; }
    IPaymentRepository Payments { get; }
    IReviewRepository Reviews { get; }
    IFavoriteRepository Favorites { get; }
}

/// <summary>
/// Identity / account aggregate repositories.
/// </summary>
public interface IIdentityUnitOfWork : IUnitOfWorkTransaction
{
    IUserRepository Users { get; }
    IVehicleRepository Vehicles { get; }
    IDeviceTokenRepository DeviceTokens { get; }
}

/// <summary>
/// Messaging aggregate repositories.
/// </summary>
public interface IMessagingUnitOfWork : IUnitOfWorkTransaction
{
    IConversationRepository Conversations { get; }
    IChatMessageRepository ChatMessages { get; }
    INotificationRepository Notifications { get; }
}

/// <summary>
/// Corporate B2B aggregate repositories.
/// </summary>
public interface ICorporateUnitOfWork : IUnitOfWorkTransaction
{
    ICompanyRepository Companies { get; }
    ICorporateBookingRepository CorporateBookings { get; }
    IEmployeeInvitationRepository EmployeeInvitations { get; }
}

/// <summary>
/// Cross-context unit of work (all aggregate-root repos).
/// Use when a use case spans contexts (e.g. account deletion) or for pipeline transactions.
/// </summary>
public interface IUnitOfWork :
    IMarketplaceUnitOfWork,
    IIdentityUnitOfWork,
    IMessagingUnitOfWork,
    ICorporateUnitOfWork,
    IDisposable
{
}
