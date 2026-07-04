using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Domain.Entities;
using ParkingApp.Domain.Entities.Corporate;
using ParkingApp.Domain.Enums;
using ParkingApp.Domain.ValueObjects;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Data;

public class ApplicationDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(255).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.RefreshToken).HasMaxLength(500);

            entity.HasMany(e => e.ParkingPasses)
                .WithOne(e => e.User)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Notification configuration
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(1000).IsRequired();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // Vehicle configuration
        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LicensePlate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Make).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Color).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Vehicles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ParkingSpace configuration
        modelBuilder.Entity<ParkingSpace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(500).IsRequired();
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.State); // Added index for State
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Country).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ZoneCode).HasMaxLength(64);
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.DailyRate).HasPrecision(18, 2);
            entity.Property(e => e.WeeklyRate).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.Amenities).HasMaxLength(1000);
            entity.Property(e => e.AllowedVehicleTypes).HasMaxLength(500);
            entity.Property(e => e.ImageUrls).HasMaxLength(4000);
            entity.Property(e => e.SpecialInstructions).HasMaxLength(2000);
            entity.Property(e => e.OwnershipType).HasConversion<int>().HasDefaultValue(ParkingSpaceOwnershipType.IndividualVendor);
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.ZoneCode);
            entity.HasIndex(e => e.CompanyOwnerId);
            entity.HasIndex(e => e.OwnershipType);
            entity.HasIndex(e => new { e.Latitude, e.Longitude });
            
            // PostGIS spatial column configuration
            entity.Property(e => e.Location)
                .HasColumnType("geography (point)");
            entity.HasIndex(e => e.Location)
                .HasMethod("gist"); // GiST index for spatial queries
            
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.ParkingSpaces)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CompanyOwner)
                .WithMany()
                .HasForeignKey(e => e.CompanyOwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ParkingAvailability configuration
        modelBuilder.Entity<ParkingAvailability>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ParkingSpaceId, e.Date });
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Availabilities)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Booking configuration
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BookingReference).HasMaxLength(50);
            entity.Property(e => e.QRCode).HasMaxLength(2000);
            entity.Property(e => e.VehicleNumber).HasMaxLength(20);
            entity.Property(e => e.VehicleModel).HasMaxLength(100);
            entity.Property(e => e.VehicleColor).HasMaxLength(50);
            entity.Property(e => e.DiscountCode).HasMaxLength(50);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.BaseAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.ServiceFee).HasPrecision(18, 2);
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
            entity.HasIndex(e => e.BookingReference).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ParkingSpaceId);
            entity.HasIndex(e => e.ParkingPassId);
            entity.HasIndex(e => new { e.StartDateTime, e.EndDateTime });
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Bookings)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingPass)
                .WithMany(p => p.Bookings)
                .HasForeignKey(e => e.ParkingPassId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ParkingPass>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ParkingZoneCode).HasMaxLength(64);
            entity.Property(e => e.CorporateBatchReference).HasMaxLength(100);
            entity.Property(e => e.DiscountPercentage).HasPrecision(5, 2);
            entity.HasIndex(e => new { e.UserId, e.ParkingSpaceId });
            entity.HasIndex(e => new { e.UserId, e.ParkingZoneCode });
            entity.HasIndex(e => e.AllocatedByUserId);
            entity.HasIndex(e => new { e.CreatedAt, e.UserId });
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.Property(e => e.CoverageType)
                .HasConversion<int>()
                .IsRequired();

            entity.OwnsOne(e => e.PassType, owned =>
            {
                owned.Property(p => p.Kind)
                    .HasColumnName("PassType")
                    .HasConversion<int>()
                    .IsRequired();
            });

            entity.OwnsOne(e => e.Duration, owned =>
            {
                owned.Property(d => d.StartDateUtc)
                    .HasColumnName("StartDateUtc")
                    .HasColumnType("timestamp with time zone")
                    .IsRequired();
                owned.Property(d => d.EndDateUtc)
                    .HasColumnName("EndDateUtc")
                    .HasColumnType("timestamp with time zone")
                    .IsRequired();
            });

            entity.OwnsOne(e => e.UsagePolicy, owned =>
            {
                owned.Property(p => p.Mode)
                    .HasColumnName("UsageMode")
                    .HasConversion<int>()
                    .IsRequired();
                owned.Property(p => p.DailyHourLimit)
                    .HasColumnName("DailyHourLimit");
            });

            entity.Navigation(e => e.PassType).IsRequired();
            entity.Navigation(e => e.Duration).IsRequired();
            entity.Navigation(e => e.UsagePolicy).IsRequired();

            entity.HasOne(e => e.User)
                .WithMany(u => u.ParkingPasses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AllocatedByUser)
                .WithMany()
                .HasForeignKey(e => e.AllocatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.ParkingPasses)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Payment configuration
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.PaymentGatewayReference).HasMaxLength(200);
            entity.Property(e => e.PaymentGateway).HasMaxLength(50);
            entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
            entity.Property(e => e.RefundReason).HasMaxLength(500);
            entity.Property(e => e.RefundTransactionId).HasMaxLength(100);
            entity.Property(e => e.ReceiptUrl).HasMaxLength(500);
            entity.Property(e => e.InvoiceNumber).HasMaxLength(50);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.Metadata).HasMaxLength(4000);
            entity.HasIndex(e => e.TransactionId);
            entity.HasIndex(e => e.BookingId);
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.Booking)
                .WithOne(b => b.Payment)
                .HasForeignKey<Payment>(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Comment).HasMaxLength(2000);
            entity.Property(e => e.OwnerResponse).HasMaxLength(1000);
            entity.HasIndex(e => e.ParkingSpaceId);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(e => !e.IsDeleted);
            
            entity.HasOne(e => e.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.Reviews)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Conversation configuration
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LastMessagePreview).HasMaxLength(100);
            entity.HasIndex(e => new { e.ParkingSpaceId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.VendorId);
            entity.HasIndex(e => e.LastMessageAt);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Vendor)
                .WithMany()
                .HasForeignKey(e => e.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingSpace)
                .WithMany()
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => new { e.ConversationId, e.CreatedAt });
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // Favorite configuration
        modelBuilder.Entity<Favorite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ParkingSpaceId }).IsUnique();
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingSpace)
                .WithMany(p => p.FavoritedBy)
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // DeviceToken configuration
        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Platform).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FcmToken).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.AppVersion).HasMaxLength(50);
            // One token row per (user, device) pair
            entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
            entity.HasIndex(e => e.FcmToken);
            entity.HasQueryFilter(e => !e.IsDeleted);

            entity.HasOne(e => e.User)
                .WithMany(u => u.DeviceTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ══════════════════════════════════════════════════════
        // CORPORATE MODULE CONFIGURATIONS
        // ══════════════════════════════════════════════════════

        // Company
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RegistrationNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(20).IsRequired();
            entity.Property(e => e.BillingAddress).HasMaxLength(500).IsRequired();

            entity.HasIndex(e => e.RegistrationNumber).IsUnique();
            entity.HasIndex(e => e.CreatedByUserId);

            entity.HasOne(e => e.CreatedByUser)
                .WithMany()
                .HasForeignKey(e => e.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted);
        });

        // UserCompanyMembership
        modelBuilder.Entity<UserCompanyMembership>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EmployeeCode).HasMaxLength(50);
            entity.Property(e => e.Priority).HasDefaultValue(1);

            entity.HasIndex(e => new { e.CompanyId, e.UserId }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.IsActive });
            entity.HasIndex(e => new { e.CompanyId, e.Role, e.IsActive });
            entity.HasIndex(e => new { e.CompanyId, e.Role, e.CreatedAt });
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Memberships)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // ParkingAllocation
        modelBuilder.Entity<ParkingAllocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.SourceType).HasConversion<int>().HasDefaultValue(ParkingAllocationSource.VendorLease);
            entity.Property(e => e.LeaseReference).HasMaxLength(100);

            // Owned: Quota
            entity.OwnsOne(e => e.Quota, q =>
            {
                q.Property(p => p.TotalSlots).HasColumnName("TotalSlots").IsRequired();
                q.Property(p => p.FixedSlots).HasColumnName("FixedSlots").IsRequired();
                q.Property(p => p.SharedSlots).HasColumnName("SharedSlots").IsRequired();
            });

            // Owned: BookingPolicy
            entity.OwnsOne(e => e.BookingPolicy, bp =>
            {
                bp.Property(p => p.MaxBookingsPerEmployeePerDay).HasColumnName("MaxBookingsPerDay").HasDefaultValue(1);
                bp.Property(p => p.MaxBookingsPerEmployeePerWeek).HasColumnName("MaxBookingsPerWeek").HasDefaultValue(5);
                bp.Property(p => p.PriorityThreshold).HasColumnName("PriorityThreshold").HasDefaultValue(1);
                bp.Property(p => p.AllowedStartTime).HasColumnName("AllowedStartTime");
                bp.Property(p => p.AllowedEndTime).HasColumnName("AllowedEndTime");
                bp.Property(p => p.AllowWeekends).HasColumnName("AllowWeekends").HasDefaultValue(false);
            });

            entity.Property(e => e.RejectionReason).HasMaxLength(500);

            entity.HasIndex(e => new { e.CompanyId, e.ParkingSpaceId });
            entity.HasIndex(e => new { e.CompanyId, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.SourceType, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.Status, e.CreatedAt });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.VendorId);

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Allocations)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ParkingSpace)
                .WithMany()
                .HasForeignKey(e => e.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedByUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // CorporateBooking
        modelBuilder.Entity<CorporateBooking>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VisitorName).HasMaxLength(200);
            entity.Property(e => e.VisitorLicensePlate).HasMaxLength(20);

            // Owned: AccessPolicy (nullable)
            entity.OwnsOne(e => e.AccessPolicy, ap =>
            {
                ap.Property(p => p.AllowedVehiclePlate).HasColumnName("AccessVehiclePlate").HasMaxLength(20);
                ap.Property(p => p.AccessStartUtc).HasColumnName("AccessStartUtc");
                ap.Property(p => p.AccessExpiryUtc).HasColumnName("AccessExpiryUtc");
                ap.Property(p => p.QrCodeToken).HasColumnName("AccessQrToken").HasMaxLength(500);
            });

            entity.HasIndex(e => new { e.CompanyId, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.SlotType });
            entity.HasIndex(e => e.MembershipId);
            entity.HasIndex(e => e.AllocationId);
            entity.HasIndex(e => e.BookingId).IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.CorporateBookings)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany(a => a.CorporateBookings)
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // FixedSlotAssignment
        modelBuilder.Entity<FixedSlotAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.SlotNumber }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId });

            entity.HasOne(e => e.Allocation)
                .WithMany(a => a.FixedAssignments)
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // EmployeeInvitation
        modelBuilder.Entity<EmployeeInvitation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(255).IsRequired();
            entity.Property(e => e.InvitationToken).HasMaxLength(500).IsRequired();

            entity.HasIndex(e => new { e.CompanyId, e.Email });
            entity.HasIndex(e => e.InvitationToken).IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Invitations)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.InvitedByUser)
                .WithMany()
                .HasForeignKey(e => e.InvitedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // CompanyUsage
        modelBuilder.Entity<CompanyUsage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalHoursUsed).HasPrecision(10, 2);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.UsageDate }).IsUnique();
            entity.HasIndex(e => new { e.CompanyId, e.UsageDate });

            entity.HasOne(e => e.Company)
                .WithMany(c => c.Usages)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany()
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });

        // CorporateWaitlistEntry
        modelBuilder.Entity<CorporateWaitlistEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VehicleNumber).HasMaxLength(20);
            entity.Property(e => e.VisitorName).HasMaxLength(200);
            entity.Property(e => e.VisitorLicensePlate).HasMaxLength(20);

            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.Status, e.PriorityAtRequest, e.CreatedAt });
            entity.HasIndex(e => new { e.CompanyId, e.MembershipId, e.Status });
            entity.HasIndex(e => new { e.CompanyId, e.AllocationId, e.RequestedStartDateTime, e.RequestedEndDateTime });

            entity.HasOne(e => e.Company)
                .WithMany(c => c.WaitlistEntries)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Membership)
                .WithMany()
                .HasForeignKey(e => e.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Allocation)
                .WithMany()
                .HasForeignKey(e => e.AllocationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.PromotedBooking)
                .WithMany()
                .HasForeignKey(e => e.PromotedBookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(e => !e.IsDeleted && (!CurrentTenantId.HasValue || e.CompanyId == CurrentTenantId.Value));
        });
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
