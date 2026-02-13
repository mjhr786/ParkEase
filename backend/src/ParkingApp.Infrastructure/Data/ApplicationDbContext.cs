using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using ParkingApp.Domain.Entities;

namespace ParkingApp.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<ParkingSpace> ParkingSpaces => Set<ParkingSpace>();
    public DbSet<ParkingAvailability> ParkingAvailabilities => Set<ParkingAvailability>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();

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
            entity.HasQueryFilter(e => !e.IsDeleted);
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
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.DailyRate).HasPrecision(18, 2);
            entity.Property(e => e.WeeklyRate).HasPrecision(18, 2);
            entity.Property(e => e.MonthlyRate).HasPrecision(18, 2);
            entity.Property(e => e.Amenities).HasMaxLength(1000);
            entity.Property(e => e.AllowedVehicleTypes).HasMaxLength(500);
            entity.Property(e => e.ImageUrls).HasMaxLength(4000);
            entity.Property(e => e.SpecialInstructions).HasMaxLength(2000);
            entity.HasIndex(e => e.City);
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
