using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Logging;

namespace ParkingApp.Notifications.Services;

/// <summary>
/// Mock SMS notification service for development and testing.
/// Replace with actual provider (Twilio, AWS SNS, etc.) in production.
/// </summary>
public class MockSmsNotificationService : ISmsNotificationService
{
    private readonly ILogger<MockSmsNotificationService> _logger;
    
    public MockSmsNotificationService(ILogger<MockSmsNotificationService> logger)
    {
        _logger = logger;
    }
    
    public async Task<SmsResult> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // Simulate network delay
        await Task.Delay(100, cancellationToken);
        
        var messageId = $"SMS-{Guid.NewGuid():N}";
        
        _logger.LogInformation(
            "SMS sent to {PhoneNumber}: {Message} (MessageId: {MessageId})", 
            MaskPhoneNumber(phoneNumber), 
            message.Length > 50 ? message[..50] + "..." : message,
            messageId);
        
        return new SmsResult(
            Success: true,
            MessageId: messageId,
            Status: SmsStatus.Sent
        );
    }
    
    public async Task<IEnumerable<SmsResult>> SendBulkAsync(IEnumerable<string> phoneNumbers, string message, CancellationToken cancellationToken = default)
    {
        var results = new List<SmsResult>();
        
        foreach (var phoneNumber in phoneNumbers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
                
            var result = await SendAsync(phoneNumber, message, cancellationToken);
            results.Add(result);
        }
        
        _logger.LogInformation("Bulk SMS sent to {Count} recipients", results.Count);
        
        return results;
    }
    
    public async Task<SmsResult> SendTemplatedAsync(string phoneNumber, string templateId, Dictionary<string, string> placeholders, CancellationToken cancellationToken = default)
    {
        // In a real implementation, fetch template from storage and replace placeholders
        var message = $"[Template: {templateId}] " + string.Join(", ", placeholders.Select(p => $"{p.Key}={p.Value}"));
        
        _logger.LogDebug("Sending templated SMS using template {TemplateId}", templateId);
        
        return await SendAsync(phoneNumber, message, cancellationToken);
    }
    
    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
            return "****";
        
        return phoneNumber[..2] + new string('*', phoneNumber.Length - 4) + phoneNumber[^2..];
    }
}

/// <summary>
/// SMS notification templates.
/// </summary>
public static class SmsTemplates
{
    public const string BookingConfirmation = "booking_confirmation";
    public const string BookingApproved = "booking_approved";
    public const string BookingRejected = "booking_rejected";
    public const string BookingCancelled = "booking_cancelled";
    public const string PaymentReceived = "payment_received";
    public const string OtpVerification = "otp_verification";
    public const string BookingReminder = "booking_reminder";
}
