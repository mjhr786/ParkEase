using Microsoft.EntityFrameworkCore;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Corporate.Domain;
using ParkingApp.Application.Interfaces;
using ParkingApp.Corporate.Contracts;

using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Corporate.Infrastructure;
using ParkingApp.Identity.Infrastructure.Persistence;
using IdentityConfigs = ParkingApp.Identity.Infrastructure.Configurations;
using ParkingApp.Messaging.Domain.Entities;
using ParkingApp.Messaging.Infrastructure.Persistence;
using MessagingConfigs = ParkingApp.Messaging.Infrastructure.Configurations;
using MarketplaceConfigs = ParkingApp.Marketplace.Infrastructure.Configurations;
using ParkingApp.Marketplace.Infrastructure.Persistence;

namespace ParkingApp.Infrastructure.Data;

/// <summary>
/// Shared database context implementing module persistence facades (Identity, Messaging, Marketplace, ΓÇª).
/// </summary>
public class ApplicationDbContext : DbContext, ParkingApp.Identity.Infrastructure.Persistence.IIdentityDbContext, ParkingApp.Messaging.Infrastructure.Persistence.IMessagingDbContext, IMarketplaceDbContext, ICorporateDbContext
{
    private readonly ICorporateTenantContext? _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICorporateTenantContext? tenantContext = null) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public Guid? CurrentTenantId => _tenantContext?.CompanyId;

    public DbSet<User> Users => Set<User>();
    public DbSet<ParkingSpace> ParkingSpaces => Set<ParkingSpace>();
    public DbSet<ParkingAvailability> ParkingAvailabilities => Set<ParkingAvailability>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<ParkingPass> ParkingPasses => Set<ParkingPass>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<LprAccessAttempt> LprAccessAttempts => Set<LprAccessAttempt>();
    public DbSet<LprCameraKey> LprCameraKeys => Set<LprCameraKey>();
    public DbSet<LprPlateRule> LprPlateRules => Set<LprPlateRule>();
    public DbSet<EventParkingPackage> EventParkingPackages => Set<EventParkingPackage>();
    public DbSet<EvChargingSession> EvChargingSessions => Set<EvChargingSession>();
    public DbSet<ParkingAncillaryService> ParkingAncillaryServices => Set<ParkingAncillaryService>();
    public DbSet<BookingAncillaryLine> BookingAncillaryLines => Set<BookingAncillaryLine>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    // Corporate Module
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompanyMembership> UserCompanyMemberships => Set<UserCompanyMembership>();
    public DbSet<ParkingAllocation> ParkingAllocations => Set<ParkingAllocation>();
    public DbSet<CorporateBooking> CorporateBookings => Set<CorporateBooking>();
    public DbSet<FixedSlotAssignment> FixedSlotAssignments => Set<FixedSlotAssignment>();
    public DbSet<EmployeeInvitation> EmployeeInvitations => Set<EmployeeInvitation>();
    public DbSet<CompanyUsage> CompanyUsages => Set<CompanyUsage>();
    public DbSet<CorporateWaitlistEntry> CorporateWaitlistEntries => Set<CorporateWaitlistEntry>();
    public DbSet<CorporateInvoice> CorporateInvoices => Set<CorporateInvoice>();
    public DbSet<CorporateInvoiceLineItem> CorporateInvoiceLineItems => Set<CorporateInvoiceLineItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Domain events are pure domain infrastructure ΓÇö never persisted.
        // Ids are always client-generated (BaseEntity.Id = Guid.NewGuid()). ValueGeneratedOnAdd
        // (EF key convention) makes navigation-discovered children track as Modified/UPDATE
        // instead of Added/INSERT ΓåÆ DbUpdateConcurrencyException ("expected 1 row, affected 0").
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Ignore(nameof(BaseEntity.DomainEvents));
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseEntity.Id))
                    .ValueGeneratedNever();
            }
        }

        // Module-owned entity configurations + host Outbox configs
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MessagingConfigs.ConversationConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityConfigs.UserConfiguration).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketplaceConfigs.ParkingSpaceConfiguration).Assembly);
        // E1: Corporate EF ownership in Corporate.Infrastructure (public module entry type for assembly scan)
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CorporateInfrastructureModule).Assembly);

        // Tenant filters need ApplicationDbContext.CurrentTenantId (not available in standalone configs).
        ApplyCorporateTenantFilters(modelBuilder);
    }

    private void ApplyCorporateTenantFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserCompanyMembership>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<ParkingAllocation>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<CorporateBooking>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<FixedSlotAssignment>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<EmployeeInvitation>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<CompanyUsage>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<CorporateWaitlistEntry>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<CorporateInvoice>()
            .HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));

        modelBuilder.Entity<CorporateInvoiceLineItem>()
            .HasQueryFilter(e => !e.IsDeleted);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
