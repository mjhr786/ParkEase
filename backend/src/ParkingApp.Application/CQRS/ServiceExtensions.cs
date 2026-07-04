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
using ParkingApp.Application.CQRS.Queries.ParkingAvailability;
using ParkingApp.Application.CQRS.Queries.Payments;
using ParkingApp.Application.CQRS.Queries.Reviews;
using ParkingApp.Application.CQRS.Commands.Favorites;
using ParkingApp.Application.CQRS.Queries.Favorites;
using ParkingApp.Application.CQRS.Commands.Notifications;
using ParkingApp.Application.CQRS.Queries.Notifications;
using ParkingApp.Application.CQRS.Commands.Vehicles;
using ParkingApp.Application.CQRS.Queries.Vehicles;
using ParkingApp.Application.CQRS.Commands.DeviceTokens;
using ParkingApp.Application.CQRS.Commands.ParkingPasses;
using ParkingApp.Application.DTOs;
using ParkingApp.Application.CQRS.Queries.ParkingPasses;

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
        services.AddScoped<IQueryHandler<GetParkingAvailabilityForecastQuery, ApiResponse<ParkingAvailabilityForecastDto>>, GetParkingAvailabilityForecastHandler>();
        services.AddScoped<IQueryHandler<GetOwnerParkingAvailabilityForecastsQuery, ApiResponse<List<ParkingAvailabilityForecastDto>>>, GetOwnerParkingAvailabilityForecastsHandler>();

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

        // ── Device Tokens ──
        services.AddScoped<ICommandHandler<RegisterDeviceTokenCommand, ApiResponse<bool>>, RegisterDeviceTokenCommandHandler>();

        // —— Parking Passes ——
        services.AddScoped<ICommandHandler<CreateParkingPassCommand, ApiResponse<ParkingPassDto>>, CreateParkingPassHandler>();
        services.AddScoped<ICommandHandler<AssignCorporatePassCommand, ApiResponse<CorporatePassAssignmentResultDto>>, AssignCorporatePassHandler>();
        services.AddScoped<IQueryHandler<GetUserActivePassQuery, ApiResponse<ActiveParkingPassesDto>>, GetUserActivePassHandler>();

        // ══════════════════════════════════════════════════════
        // CORPORATE MODULE
        // ══════════════════════════════════════════════════════
        
        // Commands
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.CreateCompanyCommand, ApiResponse<ParkingApp.Application.DTOs.CompanyDto>>, ParkingApp.Application.CQRS.Commands.Corporate.CreateCompanyHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.AddMemberCommand, ApiResponse<ParkingApp.Application.DTOs.MembershipDto>>, ParkingApp.Application.CQRS.Commands.Corporate.AddMemberHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.InviteMemberCommand, ApiResponse<ParkingApp.Application.DTOs.InvitationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.InviteMemberHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.AcceptInvitationCommand, ApiResponse<ParkingApp.Application.DTOs.MembershipDto>>, ParkingApp.Application.CQRS.Commands.Corporate.AcceptInvitationHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.RemoveMemberCommand, ApiResponse<bool>>, ParkingApp.Application.CQRS.Commands.Corporate.RemoveMemberHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.CreateCorporateParkingSpaceCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateParkingSpaceDto>>, ParkingApp.Application.CQRS.Commands.Corporate.CreateCorporateParkingSpaceHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.ToggleCorporateParkingSpaceCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateParkingSpaceDto>>, ParkingApp.Application.CQRS.Commands.Corporate.ToggleCorporateParkingSpaceHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.UpdateCorporateParkingSpaceCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateParkingSpaceDto>>, ParkingApp.Application.CQRS.Commands.Corporate.UpdateCorporateParkingSpaceHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.RetireCorporateParkingSpaceCommand, ApiResponse<bool>>, ParkingApp.Application.CQRS.Commands.Corporate.RetireCorporateParkingSpaceHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.AllocateParkingSlotsCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.AllocateParkingSlotsHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.CreateOwnedParkingAllocationCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.CreateOwnedParkingAllocationHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.ApproveAllocationCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.ApproveAllocationHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.RejectAllocationCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.RejectAllocationHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.BookCorporateParkingCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateReservationResultDto>>, ParkingApp.Application.CQRS.Commands.Corporate.BookCorporateParkingHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.BookVisitorParkingCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateReservationResultDto>>, ParkingApp.Application.CQRS.Commands.Corporate.BookVisitorParkingHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.UpdateBookingPolicyCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.UpdateBookingPolicyHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.AssignFixedSlotCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.AssignFixedSlotHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.RemoveFixedSlotCommand, ApiResponse<ParkingApp.Application.DTOs.ParkingAllocationDto>>, ParkingApp.Application.CQRS.Commands.Corporate.RemoveFixedSlotHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.CancelWaitlistEntryCommand, ApiResponse<bool>>, ParkingApp.Application.CQRS.Commands.Corporate.CancelWaitlistEntryHandler>();
        services.AddScoped<ICommandHandler<ParkingApp.Application.CQRS.Commands.Corporate.PromoteWaitlistEntryCommand, ApiResponse<ParkingApp.Application.DTOs.CorporateReservationResultDto>>, ParkingApp.Application.CQRS.Commands.Corporate.PromoteWaitlistEntryHandler>();

        // Queries
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetMyCompaniesQuery, ApiResponse<List<ParkingApp.Application.DTOs.CompanyDto>>>, ParkingApp.Application.CQRS.Queries.Corporate.GetMyCompaniesHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyDashboardQuery, ApiResponse<ParkingApp.Application.DTOs.CompanyDashboardDto>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyDashboardHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetMemberBookingsQuery, ApiResponse<ParkingApp.Application.DTOs.MemberBookingsDto>>, ParkingApp.Application.CQRS.Queries.Corporate.GetMemberBookingsHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyWaitlistQuery, ApiResponse<List<ParkingApp.Application.DTOs.CorporateWaitlistDto>>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyWaitlistHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyAllocationsQuery, ApiResponse<List<ParkingApp.Application.DTOs.ParkingAllocationDto>>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyAllocationsHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyParkingSpacesQuery, ApiResponse<List<ParkingApp.Application.DTOs.CorporateParkingSpaceDto>>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyParkingSpacesHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetVendorAllocationsQuery, ApiResponse<List<ParkingApp.Application.DTOs.VendorParkingAllocationDto>>>, ParkingApp.Application.CQRS.Queries.Corporate.GetVendorAllocationsHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyMembersQuery, ApiResponse<ParkingApp.Application.DTOs.CompanyMembersDto>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyMembersHandler>();
        services.AddScoped<IQueryHandler<ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyDetailsQuery, ApiResponse<ParkingApp.Application.DTOs.CompanyDto>>, ParkingApp.Application.CQRS.Queries.Corporate.GetCompanyDetailsHandler>();

        return services;
    }
}

