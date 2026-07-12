namespace ParkingApp.Application.DTOs;

public enum OutboxMessageStatusDto
{
    Pending = 0,
    Processing = 1,
    Processed = 2,
    Failed = 3
}

public record OutboxMessageDto(
    Guid Id,
    string TypeName,
    string ShortTypeName,
    string IdempotencyKey,
    OutboxMessageStatusDto Status,
    int AttemptCount,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? AvailableAfterUtc,
    DateTime? ProcessedAtUtc,
    string? PayloadPreview
);

public record OutboxSummaryDto(
    int Pending,
    int Processing,
    int Processed,
    int Failed,
    int Total
);

public record OutboxMessageListResultDto(
    List<OutboxMessageDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    OutboxSummaryDto Summary
);

public record ProcessOutboxResultDto(
    int ProcessedCount,
    string Message
);
