using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.CQRS.Commands.Payments;
using ParkingApp.Application.CQRS.Commands.Reviews;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.CQRS.Queries.Payments;
using ParkingApp.Application.CQRS.Queries.Reviews;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS;

public static class CQRSServiceExtensions
{
    public static IServiceCollection AddCQRS(this IServiceCollection services)
    {
        // Register Dispatcher
        services.AddScoped<IDispatcher, Dispatcher>();

        // ══════════════════════════════════════════════════════
        // COMMAND HANDLERS
        // ══════════════════════════════════════════════════════

        // ── Auth ──
        services.AddScoped<ICommandHandler<RegisterCommand, ApiResponse<TokenDto>>, RegisterHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, ApiResponse<TokenDto>>, LoginHandler>();
        services.AddScoped<ICommandHandler<RefreshTokenCommand, ApiResponse<TokenDto>>, RefreshTokenHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand, ApiResponse<bool>>, LogoutHandler>();
        services.AddScoped<ICommandHandler<ChangePasswordCommand, ApiResponse<bool>>, ChangePasswordHandler>();

        // ── Users ──
        services.AddScoped<ICommandHandler<UpdateUserCommand, ApiResponse<UserDto>>, UpdateUserHandler>();
        services.AddScoped<ICommandHandler<DeleteUserCommand, ApiResponse<bool>>, DeleteUserHandler>();

        // ── Booking ──
        services.AddScoped<ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>, CreateBookingHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>, CancelBookingHandler>();
        services.AddScoped<ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>, ApproveBookingHandler>();
        services.AddScoped<ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>, RejectBookingHandler>();
        services.AddScoped<ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>, CheckInHandler>();
        services.AddScoped<ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>, CheckOutHandler>();

        // ── Parking ──
        services.AddScoped<ICommandHandler<CreateParkingCommand, ApiResponse<ParkingSpaceDto>>, CreateParkingHandler>();
        services.AddScoped<ICommandHandler<UpdateParkingCommand, ApiResponse<ParkingSpaceDto>>, UpdateParkingHandler>();
        services.AddScoped<ICommandHandler<DeleteParkingCommand, ApiResponse<bool>>, DeleteParkingHandler>();
        services.AddScoped<ICommandHandler<ToggleActiveParkingCommand, ApiResponse<bool>>, ToggleActiveParkingHandler>();

        // ── Payments ──
        services.AddScoped<ICommandHandler<ProcessPaymentCommand, ApiResponse<PaymentResultDto>>, ProcessPaymentHandler>();
        services.AddScoped<ICommandHandler<CreatePaymentOrderCommand, ApiResponse<string>>, CreatePaymentOrderHandler>();
        services.AddScoped<ICommandHandler<VerifyPaymentCommand, ApiResponse<PaymentResultDto>>, VerifyPaymentHandler>();
        services.AddScoped<ICommandHandler<ProcessRefundCommand, ApiResponse<RefundResultDto>>, ProcessRefundHandler>();

        // ── Reviews ──
        services.AddScoped<ICommandHandler<CreateReviewCommand, ApiResponse<ReviewDto>>, CreateReviewHandler>();
        services.AddScoped<ICommandHandler<UpdateReviewCommand, ApiResponse<ReviewDto>>, UpdateReviewHandler>();
        services.AddScoped<ICommandHandler<DeleteReviewCommand, ApiResponse<bool>>, DeleteReviewHandler>();
        services.AddScoped<ICommandHandler<AddOwnerResponseCommand, ApiResponse<ReviewDto>>, AddOwnerResponseHandler>();

        // ══════════════════════════════════════════════════════
        // QUERY HANDLERS
        // ══════════════════════════════════════════════════════

        // ── Users ──
        services.AddScoped<IQueryHandler<GetCurrentUserQuery, ApiResponse<UserDto>>, GetCurrentUserHandler>();

        // ── Booking ──
        services.AddScoped<IQueryHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>, GetBookingByIdHandler>();
        services.AddScoped<IQueryHandler<GetBookingByReferenceQuery, ApiResponse<BookingDto>>, GetBookingByReferenceHandler>();
        services.AddScoped<IQueryHandler<GetUserBookingsQuery, ApiResponse<BookingListResultDto>>, GetUserBookingsHandler>();
        services.AddScoped<IQueryHandler<GetVendorBookingsQuery, ApiResponse<BookingListResultDto>>, GetVendorBookingsHandler>();
        services.AddScoped<IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>, CalculatePriceHandler>();

        // ── Parking ──
        services.AddScoped<IQueryHandler<GetParkingByIdQuery, ApiResponse<ParkingSpaceDto>>, GetParkingByIdHandler>();
        services.AddScoped<IQueryHandler<GetOwnerParkingsQuery, ApiResponse<List<ParkingSpaceDto>>>, GetOwnerParkingsHandler>();
        services.AddScoped<IQueryHandler<SearchParkingQuery, ApiResponse<ParkingSearchResultDto>>, SearchParkingHandler>();
        services.AddScoped<IQueryHandler<GetMapCoordinatesQuery, ApiResponse<List<ParkingMapDto>>>, GetMapCoordinatesHandler>();

        // ── Payments ──
        services.AddScoped<IQueryHandler<GetPaymentByIdQuery, ApiResponse<PaymentDto>>, GetPaymentByIdHandler>();
        services.AddScoped<IQueryHandler<GetPaymentByBookingIdQuery, ApiResponse<PaymentDto>>, GetPaymentByBookingIdHandler>();

        // ── Reviews ──
        services.AddScoped<IQueryHandler<GetReviewByIdQuery, ApiResponse<ReviewDto>>, GetReviewByIdHandler>();
        services.AddScoped<IQueryHandler<GetReviewsByParkingSpaceQuery, ApiResponse<List<ReviewDto>>>, GetReviewsByParkingSpaceHandler>();

        // ── Dashboard ──
        services.AddScoped<IQueryHandler<GetVendorDashboardQuery, ApiResponse<VendorDashboardDto>>, GetVendorDashboardHandler>();
        services.AddScoped<IQueryHandler<GetMemberDashboardQuery, ApiResponse<MemberDashboardDto>>, GetMemberDashboardHandler>();

        return services;
    }
}

