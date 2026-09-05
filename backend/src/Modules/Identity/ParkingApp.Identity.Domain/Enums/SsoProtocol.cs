namespace ParkingApp.Identity.Domain.Enums;

/// <summary>
/// Protocol discriminator for <see cref="Entities.CorporateSsoIdentityLink"/>.
/// Mirrors Corporate.Domain.Enums.SsoProtocol values (no cross-module Domain ref).
/// </summary>
public enum SsoProtocol : short
{
    Oidc = 1,
    Saml = 2
}
