namespace ParkingApp.Messaging.Contracts.Enums;

/// <summary>In-app / push notification category (Messaging module public enum).</summary>
public enum NotificationType
{
    BookingRequest = 0,
    BookingConfirmed = 1,
    BookingRejected = 2,
    PaymentReceived = 3,
    NewMessage = 4,
    SystemAlert = 5
}

public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}
