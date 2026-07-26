using ParkingApp.Marketplace.Application.Interfaces;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>
/// Generates a simple ParkEase event parking ticket PDF with access-pass QR.
/// </summary>
internal sealed class EventPackageTicketPdfService : IEventPackageTicketPdfService
{
    static EventPackageTicketPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] GenerateTicketPdf(EventPackageTicketContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (string.IsNullOrWhiteSpace(content.AccessPassToken))
            throw new ArgumentException("Access pass token is required.", nameof(content));

        byte[] qrPng;
        using (var generator = new QRCodeGenerator())
        using (var data = generator.CreateQrCode(content.AccessPassToken.Trim(), QRCodeGenerator.ECCLevel.Q))
        {
            var png = new PngByteQRCode(data);
            qrPng = png.GetGraphic(8);
        }

        var eventLabel = !string.IsNullOrWhiteSpace(content.EventName)
            ? content.EventName
            : content.EventTitle;
        var zone = string.IsNullOrWhiteSpace(content.ZoneName) ? null : content.ZoneName.Trim();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Text("ParkEase").Bold().FontSize(16).FontColor(Colors.Indigo.Medium);
                    col.Item().Text("Event Parking Ticket").FontSize(11).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text(eventLabel).Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(content.VenueName))
                        col.Item().Text($"Venue: {content.VenueName}");
                    if (zone is not null)
                        col.Item().Text($"Zone: {zone}").Bold();
                    col.Item().Text($"Lot: {content.ParkingSpaceTitle}");
                    if (!string.IsNullOrWhiteSpace(content.ParkingAddress))
                        col.Item().Text(content.ParkingAddress).FontSize(9).FontColor(Colors.Grey.Medium);

                    col.Item().PaddingTop(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

                    col.Item().Text($"Showtime: {Fmt(content.EventStartUtc)} → {Fmt(content.EventEndUtc)}");
                    col.Item().Text($"Access: {Fmt(content.AccessStartUtc)} → {Fmt(content.AccessEndUtc)}").Bold();
                    col.Item().Text($"Reference: {content.BookingReference}").FontSize(11);
                    if (!string.IsNullOrWhiteSpace(content.VehicleNumber))
                        col.Item().Text($"Vehicle: {content.VehicleNumber}");
                    col.Item().Text($"Paid: ₹{content.TotalAmount:0.00}");

                    col.Item().PaddingTop(8).AlignCenter().Width(120).Image(qrPng);
                    col.Item().AlignCenter().Text(content.AccessPassToken)
                        .FontSize(7)
                        .FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text("Show this QR at the gate · ParkEase").FontSize(8);
            });
        }).GeneratePdf();
    }

    private static string Fmt(DateTime utc) =>
        utc.ToUniversalTime().ToString("dd MMM yyyy HH:mm") + " UTC";
}
