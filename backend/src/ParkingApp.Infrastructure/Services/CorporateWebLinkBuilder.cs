using Microsoft.Extensions.Configuration;
using ParkingApp.Application.Interfaces;

namespace ParkingApp.Infrastructure.Services;

public sealed class CorporateWebLinkBuilder : ICorporateWebLinkBuilder
{
    private readonly string _publicWebBaseUrl;

    public CorporateWebLinkBuilder(IConfiguration configuration)
    {
        _publicWebBaseUrl = (configuration["Corporate:PublicWebBaseUrl"]
            ?? configuration["App:PublicWebBaseUrl"]
            ?? "http://localhost:5173").TrimEnd('/');
    }

    public string BuildInviteAcceptUrl(string invitationToken)
    {
        if (string.IsNullOrWhiteSpace(invitationToken))
        {
            throw new ArgumentException("Invitation token is required.", nameof(invitationToken));
        }

        return $"{_publicWebBaseUrl}/invite/accept/{Uri.EscapeDataString(invitationToken.Trim())}";
    }
}
