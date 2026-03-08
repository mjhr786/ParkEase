using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS.Commands.Auth;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Handlers.Bookings;
using ParkingApp.Application.CQRS.Commands.Chat;
using ParkingApp.Application.CQRS.Commands.Parking;
using ParkingApp.Application.CQRS.Commands.Payments;
using ParkingApp.Application.CQRS.Commands.Reviews;
using ParkingApp.Application.CQRS.Commands.Users;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.CQRS.Queries.Chat;
using ParkingApp.Application.CQRS.Queries.Dashboard;
using ParkingApp.Application.CQRS.Queries.Parking;
using ParkingApp.Application.CQRS.Queries.Payments;
using ParkingApp.Application.CQRS.Queries.Reviews;
using ParkingApp.Application.CQRS.Commands.Favorites;
using ParkingApp.Application.CQRS.Queries.Favorites;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Application.CQRS.Queries.Notifications;
using ParkingApp.Application.CQRS.Commands.Vehicles;
using ParkingApp.Application.CQRS.Queries.Vehicles;
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
        services.AddScoped<ICommandHandler<UpdateBookingCommand, ApiResponse<BookingDto>>, UpdateBookingHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>, CancelBookingHandler>();
        services.AddScoped<ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>, ApproveBookingHandler>();
        services.AddScoped<ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>, RejectBookingHandler>();
        services.AddScoped<ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>, CheckInHandler>();
        services.AddScoped<ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>, CheckOutHandler>();
        services.AddScoped<ICommandHandler<RequestExtensionCommand, ApiResponse<BookingDto>>, RequestExtensionHandler>();
        services.AddScoped<ICommandHandler<ApproveExtensionCommand, ApiResponse<BookingDto>>, ApproveExtensionHandler>();
        services.AddScoped<ICommandHandler<RejectExtensionCommand, ApiResponse<BookingDto>>, RejectExtensionHandler>();
        services.AddScoped<ICommandHandler<ConfirmExtensionPaymentCommand, ApiResponse<BookingDto>>, ConfirmExtensionPaymentHandler>();

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

        // ── Chat ──
        services.AddScoped<ICommandHandler<SendMessageCommand, ApiResponse<ChatMessageDto>>, SendMessageHandler>();
        services.AddScoped<ICommandHandler<MarkMessagesReadCommand, ApiResponse<bool>>, MarkMessagesReadHandler>();

        // ── Favorites ──
        services.AddScoped<ICommandHandler<ToggleFavoriteCommand, ApiResponse<bool>>, ToggleFavoriteCommandHandler>();

        // ── Vehicles ──
        services.AddScoped<ICommandHandler<CreateVehicleCommand, ApiResponse<VehicleDto>>, CreateVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateVehicleCommand, ApiResponse<VehicleDto>>, UpdateVehicleCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteVehicleCommand, ApiResponse<bool>>, DeleteVehicleCommandHandler>();

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
        services.AddScoped<IQueryHandler<GetBookingsByParkingSpaceQuery, ApiResponse<BookingListResultDto>>, GetBookingsByParkingSpaceHandler>();
        services.AddScoped<IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>, CalculatePriceHandler>();
        services.AddScoped<IQueryHandler<GetPendingRequestsCountQuery, ApiResponse<int>>, GetPendingRequestsCountHandler>();

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

        // ── Chat ──
        services.AddScoped<IQueryHandler<GetConversationsQuery, ApiResponse<ConversationListDto>>, GetConversationsHandler>();
        services.AddScoped<IQueryHandler<GetMessagesQuery, ApiResponse<List<ChatMessageDto>>>, GetMessagesHandler>();

        // ── Favorites ──
        services.AddScoped<IQueryHandler<GetMyFavoritesQuery, ApiResponse<IEnumerable<ParkingSpaceDto>>>, GetMyFavoritesQueryHandler>();

        // ── Notifications ──
        services.AddScoped<IQueryHandler<GetMyNotificationsQuery, ApiResponse<NotificationListDto>>, GetMyNotificationsQueryHandler>();
        services.AddScoped<ICommandHandler<MarkNotificationAsReadCommand, ApiResponse<bool>>, MarkNotificationAsReadCommandHandler>();
        services.AddScoped<ICommandHandler<MarkAllNotificationsAsReadCommand, ApiResponse<bool>>, MarkAllNotificationsAsReadCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteNotificationCommand, ApiResponse<bool>>, DeleteNotificationCommandHandler>();
        services.AddScoped<ICommandHandler<ClearAllNotificationsCommand, ApiResponse<bool>>, ClearAllNotificationsCommandHandler>();

        // ── Vehicles ──
        services.AddScoped<IQueryHandler<GetMyVehiclesQuery, ApiResponse<IEnumerable<VehicleDto>>>, GetMyVehiclesQueryHandler>();

        return services;
    }
}

