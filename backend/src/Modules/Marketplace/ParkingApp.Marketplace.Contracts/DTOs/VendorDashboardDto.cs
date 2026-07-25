using System.Collections.Generic;

namespace ParkingApp.Marketplace.Contracts.DTOs;

public record VendorDashboardDto(
    int TotalParkingSpaces,
    int ActiveParkingSpaces,
    int TotalBookings,
    int ActiveBookings,
    int PendingBookings,
    int CompletedBookings,
    decimal TotalEarnings,
    decimal MonthlyEarnings,
    decimal WeeklyEarnings,
    double AverageRating,
    int TotalReviews,
    List<BookingDto> RecentBookings,
    List<DashboardChartDataDto> ChartData
);

public record MemberDashboardDto(
    int TotalBookings,
    int ActiveBookings,
    int CompletedBookings,
    decimal TotalSpent,
    List<BookingDto> UpcomingBookings,
    List<BookingDto> RecentBookings
);

public record DashboardChartDataDto(
    string Label,
    decimal Earnings,
    int Bookings
);

