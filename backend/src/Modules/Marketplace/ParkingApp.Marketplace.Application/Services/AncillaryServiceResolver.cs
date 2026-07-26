using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Interfaces;

namespace ParkingApp.Marketplace.Application.Services;

/// <summary>
/// Loads and validates catalog add-ons for quote / booking create.
/// </summary>
internal static class AncillaryServiceResolver
{
    public sealed record ResolveResult(
        bool Success,
        string? ErrorMessage,
        decimal Subtotal,
        IReadOnlyList<BookingAncillaryLineDto> QuoteLines,
        IReadOnlyList<ParkingAncillaryService> Services);

    public static async Task<ResolveResult> ResolveForBookingAsync(
        IMarketplaceUnitOfWork unitOfWork,
        Guid parkingSpaceId,
        IReadOnlyList<Guid>? serviceIds,
        bool requireAllActive = true,
        CancellationToken cancellationToken = default)
    {
        var distinctIds = (serviceIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
        {
            return new ResolveResult(
                true,
                null,
                0m,
                Array.Empty<BookingAncillaryLineDto>(),
                Array.Empty<ParkingAncillaryService>());
        }

        var services = await unitOfWork.ParkingAncillaryServices.GetByIdsForSpaceAsync(
            parkingSpaceId,
            distinctIds,
            activeOnly: requireAllActive,
            cancellationToken);

        if (services.Count != distinctIds.Count)
        {
            return new ResolveResult(
                false,
                "One or more selected add-on services are unavailable for this parking space.",
                0m,
                Array.Empty<BookingAncillaryLineDto>(),
                Array.Empty<ParkingAncillaryService>());
        }

        // Preserve guest selection order
        var ordered = distinctIds
            .Select(id => services.First(s => s.Id == id))
            .ToList();

        var lines = ordered
            .Select(s => new BookingAncillaryLineDto(
                Guid.Empty,
                s.Id,
                s.Name,
                s.Price,
                1,
                s.Price))
            .ToList();

        var subtotal = Math.Round(lines.Sum(l => l.LineTotal), 2, MidpointRounding.AwayFromZero);
        return new ResolveResult(true, null, subtotal, lines, ordered);
    }

    public static ParkingAncillaryServiceDto ToDto(ParkingAncillaryService service) => new(
        service.Id,
        service.ParkingSpaceId,
        service.Name,
        service.Description,
        service.Price,
        service.DurationMinutes,
        service.IsActive,
        service.SortOrder,
        service.CreatedAt);

    public static BookingAncillaryLineDto ToLineDto(BookingAncillaryLine line) => new(
        line.Id,
        line.ServiceId,
        line.SnapshotName,
        line.UnitPrice,
        line.Quantity,
        line.LineTotal);
}
