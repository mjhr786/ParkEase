using System.Text.Json.Serialization;
using ParkingApp.Application.DTOs;
using ParkingApp.Marketplace.Contracts.DTOs;
using ParkingApp.Identity.Application.DTOs;

namespace ParkingApp.API.Serialization;

/// <summary>
/// Source-generated JSON serializer context for high-throughput DTO responses.
/// Eliminates reflection overhead and lowers GC allocations on hot paths.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(ApiResponse<object>))]
[JsonSerializable(typeof(ApiResponse<string>))]
[JsonSerializable(typeof(ApiResponse<bool>))]
[JsonSerializable(typeof(ApiResponse<Guid>))]
[JsonSerializable(typeof(ApiResponse<ParkingSpaceDto>))]
[JsonSerializable(typeof(ApiResponse<ParkingSearchResultDto>))]
[JsonSerializable(typeof(ApiResponse<List<ParkingSpaceDto>>))]
[JsonSerializable(typeof(ApiResponse<List<ParkingMapDto>>))]
[JsonSerializable(typeof(ApiResponse<UserDto>))]
[JsonSerializable(typeof(ApiResponse<BookingDto>))]
[JsonSerializable(typeof(PaginatedResponse<ParkingSpaceDto>))]
public partial class ParkEaseJsonContext : JsonSerializerContext
{
}
