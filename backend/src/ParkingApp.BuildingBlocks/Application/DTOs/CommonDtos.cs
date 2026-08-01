namespace ParkingApp.Application.DTOs;

// Common DTOs
// Code (KD-20): optional machine-readable reason (e.g. "channel_forbidden"). Positional 3–4 arg call sites remain valid.
public record ApiResponse<T>(
    bool Success,
    string? Message,
    T? Data,
    List<string>? Errors = null,
    string? Code = null
);

public record PaginatedResponse<T>(
    List<T> Data,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
