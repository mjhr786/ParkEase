using FluentAssertions;
using ParkingApp.Marketplace.Infrastructure.ReadModel.Bookings;

namespace ParkingApp.UnitTests.Bookings;

/// <summary>
/// KD-19 list-filter contracts without a live DB.
/// Response: full GetUserBookingsAsync/GetVendorBookingsAsync Dapper integration would need
/// PostgreSQL + seed data; these constants are the exact baseWhere clauses wired into BookingReadStore.
/// </summary>
public class BookingListSqlFiltersTests
{
    [Fact]
    public void ConsumerUserBookings_ExcludesCorporateStaged()
    {
        BookingListSqlFilters.ConsumerUserBookings.Should().Contain("""b."UserId" = @UserId""");
        BookingListSqlFilters.ConsumerUserBookings.Should().Contain("""b."IsDeleted" = FALSE""");
        BookingListSqlFilters.ConsumerUserBookings.Should().Contain("""b."IsCorporateStaged" = FALSE""");
        BookingListSqlFilters.ConsumerUserBookings.Should().NotContain("CorporateBookings");
    }

    [Fact]
    public void VendorBookings_DoesNotExcludeCorporateStaged()
    {
        BookingListSqlFilters.VendorBookings.Should().Contain("""ps."OwnerId" = @VendorId""");
        BookingListSqlFilters.VendorBookings.Should().Contain("""b."IsDeleted" = FALSE""");
        BookingListSqlFilters.VendorBookings.Should().NotContain("IsCorporateStaged");
        BookingListSqlFilters.VendorBookings.Should().NotContain("CorporateBookings");
    }
}
