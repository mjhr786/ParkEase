namespace ParkingApp.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    IParkingSpaceRepository ParkingSpaces { get; }
    IBookingRepository Bookings { get; }
    IParkingPassRepository ParkingPasses { get; }
    IPaymentRepository Payments { get; }
    IReviewRepository Reviews { get; }
    IConversationRepository Conversations { get; }
    IChatMessageRepository ChatMessages { get; }
    IFavoriteRepository Favorites { get; }
    INotificationRepository Notifications { get; }
    IVehicleRepository Vehicles { get; }
    IDeviceTokenRepository DeviceTokens { get; }

    // Corporate Module
    ICompanyRepository Companies { get; }
    ICorporateBookingRepository CorporateBookings { get; }
    IEmployeeInvitationRepository EmployeeInvitations { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
