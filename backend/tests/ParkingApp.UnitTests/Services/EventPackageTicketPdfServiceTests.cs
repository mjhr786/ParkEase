using FluentAssertions;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Services;

public class EventPackageTicketPdfServiceTests
{
    [Fact]
    public void GenerateTicketPdf_Produces_NonEmpty_Pdf()
    {
        var svc = new EventPackageTicketPdfService();
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        var bytes = svc.GenerateTicketPdf(new EventPackageTicketContent(
            BookingReference: "EVT-TEST01",
            AccessPassToken: "PE-EVT-TEST01-ABCDEF1234567890",
            EventTitle: "Concert Night",
            EventName: "Big Show",
            VenueName: "Arena",
            ZoneName: "North Lot",
            ParkingSpaceTitle: "Lot A",
            ParkingAddress: "1 Main St",
            EventStartUtc: start,
            EventEndUtc: end,
            AccessStartUtc: start.AddMinutes(-60),
            AccessEndUtc: end.AddMinutes(30),
            VehicleNumber: "KA01AB1234",
            TotalAmount: 615m));

        bytes.Should().NotBeNullOrEmpty();
        // PDF magic header
        System.Text.Encoding.ASCII.GetString(bytes.AsSpan(0, 4)).Should().Be("%PDF");
    }
}
