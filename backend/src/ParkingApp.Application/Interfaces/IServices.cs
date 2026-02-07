using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<TokenDto>> RegisterAsync(RegisterDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<TokenDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<TokenDto>> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> LogoutAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<ApiResponse<UserDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IParkingSpaceService
{
    Task<ApiResponse<ParkingSpaceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<ParkingSearchResultDto>> SearchAsync(ParkingSearchDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ParkingSpaceDto>>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ParkingSpaceDto>> CreateAsync(Guid ownerId, CreateParkingSpaceDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<ParkingSpaceDto>> UpdateAsync(Guid id, Guid ownerId, UpdateParkingSpaceDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ToggleActiveAsync(Guid id, Guid ownerId, CancellationToken cancellationToken = default);
}

public interface IBookingService
{
    Task<ApiResponse<BookingDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingListResultDto>> GetByUserAsync(Guid userId, BookingFilterDto? filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingListResultDto>> GetByParkingSpaceAsync(Guid parkingSpaceId, Guid ownerId, BookingFilterDto? filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingListResultDto>> GetVendorBookingsAsync(Guid vendorId, BookingFilterDto? filter, CancellationToken cancellationToken = default);
    Task<ApiResponse<PriceBreakdownDto>> CalculatePriceAsync(PriceCalculationDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> CreateAsync(Guid userId, CreateBookingDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> UpdateAsync(Guid id, Guid userId, UpdateBookingDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> CancelAsync(Guid id, Guid userId, CancelBookingDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> ApproveAsync(Guid id, Guid vendorId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> RejectAsync(Guid id, Guid vendorId, string? reason, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> CheckInAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<BookingDto>> CheckOutAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}

public interface IPaymentAppService
{
    Task<ApiResponse<PaymentDto>> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaymentDto>> GetByBookingIdAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<PaymentResultDto>> ProcessPaymentAsync(Guid userId, CreatePaymentDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<RefundResultDto>> ProcessRefundAsync(Guid userId, RefundRequestDto dto, CancellationToken cancellationToken = default);
}

public interface IReviewService
{
    Task<ApiResponse<ReviewDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ReviewDto>>> GetByParkingSpaceAsync(Guid parkingSpaceId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewDto>> CreateAsync(Guid userId, CreateReviewDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewDto>> UpdateAsync(Guid id, Guid userId, UpdateReviewDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<ReviewDto>> AddOwnerResponseAsync(Guid reviewId, Guid ownerId, OwnerResponseDto dto, CancellationToken cancellationToken = default);
}

public interface IDashboardService
{
    Task<ApiResponse<VendorDashboardDto>> GetVendorDashboardAsync(Guid vendorId, CancellationToken cancellationToken = default);
    Task<ApiResponse<MemberDashboardDto>> GetMemberDashboardAsync(Guid memberId, CancellationToken cancellationToken = default);
}
