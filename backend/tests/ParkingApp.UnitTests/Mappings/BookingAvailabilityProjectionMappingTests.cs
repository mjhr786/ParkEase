using FluentAssertions;
using ParkingApp.Marketplace.Application.Mappings;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Models;
using Xunit;

namespace ParkingApp.UnitTests.Mappings;

/// <summary>
/// The Booking overload delegates to the discovery projection. Both must produce the same availability.
/// </summary>
public class BookingAvailabilityProjectionMappingTests
{
    [Fact]
    public void FullBookingAndNarrowRead_ProduceTheSameReservationsAndPrice()
    {
        var now = DateTime.UtcNow;
        var spaceId = Guid.NewGuid();
        var parking = PricedSpace(spaceId);
        var bookings = new List<Booking>
        {
            Row(spaceId, BookingStatus.InProgress, now.AddMinutes(-5), now.AddHours(2), 2),
            Row(spaceId, BookingStatus.Confirmed, now.AddMinutes(-30), now.AddHours(1), 1),
            Row(spaceId, BookingStatus.Pending, now.AddHours(3), now.AddHours(4), null),
            Row(spaceId, BookingStatus.AwaitingPayment, now.AddHours(4), now.AddHours(5), 4),
            Row(spaceId, BookingStatus.PendingExtension, now.AddHours(5), now.AddHours(6), 5),
            Row(spaceId, BookingStatus.AwaitingExtensionPayment, now.AddHours(6), now.AddHours(7), 6),
            Row(spaceId, BookingStatus.Completed, now.AddHours(1), now.AddHours(2), 9),
            Row(spaceId, BookingStatus.Cancelled, now.AddHours(1), now.AddHours(2), 9),
            Row(spaceId, BookingStatus.Expired, now.AddHours(1), now.AddHours(2), 9),
            Row(spaceId, BookingStatus.Rejected, now.AddHours(1), now.AddHours(2), 9),
            Row(spaceId, BookingStatus.Confirmed, now.AddHours(-3), now.AddMinutes(-5), 8),
            Row(spaceId, BookingStatus.Confirmed, now.AddMinutes(-20), now.AddSeconds(30), 3)
        };
        var reads = bookings.Select(Read).ToList();

        var fromEntities = parking.ToDtoWithFullDetails(bookings, priceAsOfUtc: now);
        var fromReads = parking.ToDtoWithFullDetails(reads, priceAsOfUtc: now);
        var fromDetail = parking.ToDtoWithReservations(bookings, now);
        var withNone = parking.ToDtoWithFullDetails(Array.Empty<BookingAvailabilityRead>(), priceAsOfUtc: now);

        fromReads.ActiveReservations.Should().BeEquivalentTo(
            fromEntities.ActiveReservations, options => options.WithStrictOrdering());
        fromDetail.ActiveReservations.Should().BeEquivalentTo(
            fromEntities.ActiveReservations, options => options.WithStrictOrdering());
        fromReads.EffectiveHourlyRate.Should().Be(fromEntities.EffectiveHourlyRate);
        fromReads.EffectiveHourlyRate.Should().NotBe(withNone.EffectiveHourlyRate);

        fromReads.ActiveReservations!.Select(r => (r.StartDateTime, r.EndDateTime, r.SlotNumber)).Should().Equal(
            (bookings[1].StartDateTime, bookings[1].EndDateTime, bookings[1].SlotNumber),
            (bookings[11].StartDateTime, bookings[11].EndDateTime, bookings[11].SlotNumber),
            (bookings[0].StartDateTime, bookings[0].EndDateTime, bookings[0].SlotNumber),
            (bookings[2].StartDateTime, bookings[2].EndDateTime, bookings[2].SlotNumber),
            (bookings[3].StartDateTime, bookings[3].EndDateTime, bookings[3].SlotNumber),
            (bookings[4].StartDateTime, bookings[4].EndDateTime, bookings[4].SlotNumber),
            (bookings[5].StartDateTime, bookings[5].EndDateTime, bookings[5].SlotNumber));
    }

    [Fact]
    public void NoBookings_UsesAvailableSpotsAndReturnsNoReservations()
    {
        var now = DateTime.UtcNow;
        var parking = PricedSpace(Guid.NewGuid());

        var fromEntities = parking.ToDtoWithFullDetails(Array.Empty<Booking>(), priceAsOfUtc: now);
        var fromReads = parking.ToDtoWithFullDetails(Array.Empty<BookingAvailabilityRead>(), priceAsOfUtc: now);

        MarketplaceMappings.EstimateAvailableAt(parking, Array.Empty<Booking>(), now).Should().Be(parking.AvailableSpots);
        MarketplaceMappings.EstimateAvailableAt(parking, Array.Empty<BookingAvailabilityRead>(), now).Should().Be(parking.AvailableSpots);
        fromReads.ActiveReservations.Should().BeEmpty();
        fromReads.EffectiveHourlyRate.Should().Be(fromEntities.EffectiveHourlyRate);
    }

    private static ParkingSpace PricedSpace(Guid id) => new()
    {
        Id = id,
        Title = "Lot",
        Description = "D",
        Address = "A",
        City = "Pune",
        State = "MH",
        Country = "IN",
        PostalCode = "411001",
        TotalSpots = 4,
        AvailableSpots = 4,
        HourlyRate = 100m,
        IsDynamicPricingEnabled = true,
        TimeZoneId = "UTC"
    };

    private static Booking Row(Guid spaceId, BookingStatus status, DateTime start, DateTime end, int? slot) => new()
    {
        ParkingSpaceId = spaceId,
        Status = status,
        StartDateTime = start,
        EndDateTime = end,
        SlotNumber = slot
    };

    private static BookingAvailabilityRead Read(Booking booking) =>
        new(booking.ParkingSpaceId, booking.Status, booking.StartDateTime, booking.EndDateTime, booking.SlotNumber);
}
