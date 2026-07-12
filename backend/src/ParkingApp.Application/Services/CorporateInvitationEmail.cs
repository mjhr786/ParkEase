using Microsoft.Extensions.Logging;
using ParkingApp.Application.Interfaces;
using ParkingApp.Domain.Enums;

namespace ParkingApp.Application.Services;

/// <summary>
/// Best-effort email helper for corporate invitation links.
/// </summary>
public static class CorporateInvitationEmail
{
    public static async Task TrySendAsync(
        IEmailService email,
        ICorporateWebLinkBuilder links,
        ILogger logger,
        string toEmail,
        string companyName,
        string invitationToken,
        CompanyRole role,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var link = links.BuildInviteAcceptUrl(invitationToken);
            var roleLabel = role == CompanyRole.Admin ? "Admin" : "Employee";
            var subject = $"You're invited to join {companyName} on ParkEase";
            var body = $"""
                <p>Hello,</p>
                <p>You have been invited to join <strong>{System.Net.WebUtility.HtmlEncode(companyName)}</strong>
                on ParkEase as <strong>{roleLabel}</strong>.</p>
                <p><a href="{link}">Accept invitation</a></p>
                <p>Or copy this link:<br/><code>{System.Net.WebUtility.HtmlEncode(link)}</code></p>
                <p>This invitation expires on {expiresAtUtc:u} (UTC).</p>
                <p>— ParkEase</p>
                """;

            await email.SendEmailAsync(toEmail, subject, body, isHtml: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send corporate invitation email to {Email}", toEmail);
        }
    }
}
