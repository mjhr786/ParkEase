using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Exceptions;

namespace ParkingApp.Application.Common;

/// <summary>
/// Maps domain exceptions to API-facing <see cref="ApiResponse{T}"/> results.
/// Handlers catch domain exceptions so CQRS still returns a structured response
/// without becoming an unhandled 500.
/// </summary>
public static class DomainExceptionMapping
{
    public static ApiResponse<T> ToFailureResponse<T>(DomainException exception)
    {
        var errors = exception switch
        {
            ValidationException validation when validation.Errors.Count > 0
                => validation.Errors.SelectMany(kvp => kvp.Value).ToList(),
            _ => new List<string> { exception.Message }
        };

        return new ApiResponse<T>(false, exception.Message, default, errors);
    }
}
