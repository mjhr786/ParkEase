namespace ParkingApp.Admin.Application.DTOs;

public sealed record AdminDashboardDto(
    int TotalUsers,
    int ActiveUsers,
    int AdminUsers,
    int TotalListings,
    int ActiveListings,
    int TotalBookings,
    int ActiveBookings,
    int CompletedBookings,
    int TotalPayments,
    decimal TotalPaymentVolume,
    decimal RefundedVolume,
    int Companies,
    int AuditEventsLast7Days,
    DateTime GeneratedAtUtc);

public sealed record AdminAuditLogListItemDto(
    Guid Id,
    DateTime OccurredAtUtc,
    Guid ActorUserId,
    string ActorEmail,
    string Action,
    string ResourceType,
    Guid? ResourceId,
    string? PayloadJson);

public sealed record AdminAuditLogPageDto(
    IReadOnlyList<AdminAuditLogListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
