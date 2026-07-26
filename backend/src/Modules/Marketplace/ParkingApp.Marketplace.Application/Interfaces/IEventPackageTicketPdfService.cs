namespace ParkingApp.Marketplace.Application.Interfaces;

public sealed record EventPackageTicketContent(
    string BookingReference,
    string AccessPassToken,
    string EventTitle,
    string? EventName,
    string? VenueName,
    string? ZoneName,
    string ParkingSpaceTitle,
    string? ParkingAddress,
    DateTime EventStartUtc,
    DateTime EventEndUtc,
    DateTime AccessStartUtc,
    DateTime AccessEndUtc,
    string? VehicleNumber,
    decimal TotalAmount);

public interface IEventPackageTicketPdfService
{
    /// <summary>Builds a scannable event parking ticket PDF (QR = access pass token).</summary>
    byte[] GenerateTicketPdf(EventPackageTicketContent content);
}
