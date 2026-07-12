namespace ParkingApp.Application.Interfaces;

/// <summary>
/// Builds absolute frontend URLs for corporate flows (invites, etc.).
/// </summary>
public interface ICorporateWebLinkBuilder
{
    string BuildInviteAcceptUrl(string invitationToken);
}
