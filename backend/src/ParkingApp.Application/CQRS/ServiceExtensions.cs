using Microsoft.Extensions.DependencyInjection;
using ParkingApp.Application.CQRS.Commands.Bookings;
using ParkingApp.Application.CQRS.Queries.Bookings;
using ParkingApp.Application.DTOs;

namespace ParkingApp.Application.CQRS;

public static class CQRSServiceExtensions
{
    public static IServiceCollection AddCQRS(this IServiceCollection services)
    {
        // Register Dispatcher
        services.AddScoped<IDispatcher, Dispatcher>();

        // Register Command Handlers
        services.AddScoped<ICommandHandler<CreateBookingCommand, ApiResponse<BookingDto>>, CreateBookingHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand, ApiResponse<BookingDto>>, CancelBookingHandler>();
        services.AddScoped<ICommandHandler<ApproveBookingCommand, ApiResponse<BookingDto>>, ApproveBookingHandler>();
        services.AddScoped<ICommandHandler<RejectBookingCommand, ApiResponse<BookingDto>>, RejectBookingHandler>();
        services.AddScoped<ICommandHandler<CheckInCommand, ApiResponse<BookingDto>>, CheckInHandler>();
        services.AddScoped<ICommandHandler<CheckOutCommand, ApiResponse<BookingDto>>, CheckOutHandler>();

        // Register Query Handlers
        services.AddScoped<IQueryHandler<GetBookingByIdQuery, ApiResponse<BookingDto>>, GetBookingByIdHandler>();
        services.AddScoped<IQueryHandler<GetBookingByReferenceQuery, ApiResponse<BookingDto>>, GetBookingByReferenceHandler>();
        services.AddScoped<IQueryHandler<GetUserBookingsQuery, ApiResponse<BookingListResultDto>>, GetUserBookingsHandler>();
        services.AddScoped<IQueryHandler<GetVendorBookingsQuery, ApiResponse<BookingListResultDto>>, GetVendorBookingsHandler>();
        services.AddScoped<IQueryHandler<CalculatePriceQuery, ApiResponse<PriceBreakdownDto>>, CalculatePriceHandler>();

        return services;
    }
}
