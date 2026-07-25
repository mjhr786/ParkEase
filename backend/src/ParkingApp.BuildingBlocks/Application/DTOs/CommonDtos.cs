namespace ParkingApp.Application.DTOs;

// Common DTOs
public record ApiResponse<T>(
    bool Success,
    string? Message,
    T? Data,
    List<string>? Errors = null
);

public record PaginatedResponse<T>(
    List<T> Data,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);
