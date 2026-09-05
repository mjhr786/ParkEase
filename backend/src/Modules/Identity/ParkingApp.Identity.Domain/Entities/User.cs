using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Exceptions;
using ParkingApp.BuildingBlocks.Security;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Identity.Domain.Enums;

namespace ParkingApp.Identity.Domain.Entities;

/// <summary>
/// Identity / user aggregate.
/// Cross-module parties (bookings, parking spaces, etc.) are referenced elsewhere by UserId only.
/// </summary>
public class User : BaseEntity
{
    /// <summary>Validated email value object (persisted as string via EF conversion).</summary>
    public Email Email { get; internal set; } = null!;

    /// <summary>Null when the account is social-only (no password set).</summary>
    public string? PasswordHash { get; internal set; }

    public string FirstName { get; internal set; } = string.Empty;
    public string LastName { get; internal set; } = string.Empty;
    public string PhoneNumber { get; internal set; } = string.Empty;
    public UserRole Role { get; internal set; }
    public bool IsEmailVerified { get; internal set; }
    public bool IsPhoneVerified { get; internal set; }
    public bool IsActive { get; internal set; } = true;
    public string? RefreshToken { get; internal set; }
    public DateTime? RefreshTokenExpiryTime { get; internal set; }
    public DateTime? LastLoginAt { get; internal set; }

    /// <summary>Last successful mint product channel (session bind for refresh). Nullable for legacy users.</summary>
    public ProductChannel? SessionChannel { get; internal set; }

    /// <summary>Last Corporate company bind (null for Marketplace/Admin/bootstrap).</summary>
    public Guid? SessionCompanyId { get; internal set; }

    /// <summary>Last Corporate company_role (Admin|Employee) for re-mint; null when unbound.</summary>
    public string? SessionCompanyRole { get; internal set; }

    // Identity-owned collections only (no Marketplace reverse navigations)
    public virtual ICollection<Vehicle> Vehicles { get; internal set; } = new List<Vehicle>();
    public virtual ICollection<DeviceToken> DeviceTokens { get; internal set; } = new List<DeviceToken>();
    public virtual ICollection<UserExternalLogin> ExternalLogins { get; internal set; } = new List<UserExternalLogin>();
    public virtual ICollection<CorporateSsoIdentityLink> CorporateSsoLinks { get; internal set; } = new List<CorporateSsoIdentityLink>();

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>True when a password hash is present (password auth is available).</summary>
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    internal User()
    {
    }

    public static User Register(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string phoneNumber,
        UserRole role = UserRole.User)
    {
        Email emailVo;
        try
        {
            emailVo = new Email(email);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("email", ex.Message);
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("passwordHash", "Password hash is required");
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ValidationException("firstName", "First name is required");
        if (string.IsNullOrWhiteSpace(lastName))
            throw new ValidationException("lastName", "Last name is required");

        return new User
        {
            Email = emailVo,
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PhoneNumber = phoneNumber?.Trim() ?? string.Empty,
            Role = role,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a Marketplace social-only user (null password). Name policy KD-SL-16:
    /// never fails solely for missing names; defaults first from email local-part or "User", last "Account".
    /// </summary>
    public static User RegisterFromExternal(
        string email,
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        bool emailVerified = false)
    {
        Email emailVo;
        try
        {
            emailVo = new Email(email);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("email", ex.Message);
        }

        var first = TrimOrNull(firstName) ?? DeriveFirstNameFromEmail(emailVo.Value) ?? "User";
        var last = TrimOrNull(lastName) ?? "Account";

        return new User
        {
            Email = emailVo,
            PasswordHash = null,
            FirstName = first,
            LastName = last,
            PhoneNumber = phoneNumber?.Trim() ?? string.Empty,
            Role = UserRole.User,
            IsEmailVerified = emailVerified,
            IsActive = true
        };
    }

    /// <summary>
    /// Creates a Corporate SSO user (null password). Distinct from Marketplace <see cref="RegisterFromExternal"/> (KD-CS-27).
    /// Name policy matches social fallbacks — never fails solely for missing names.
    /// Role is always User (never Admin via SSO).
    /// </summary>
    public static User RegisterFromCorporateSso(
        string email,
        string? firstName = null,
        string? lastName = null,
        bool emailVerified = false)
    {
        Email emailVo;
        try
        {
            emailVo = new Email(email);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationException("email", ex.Message);
        }

        var first = TrimOrNull(firstName) ?? DeriveFirstNameFromEmail(emailVo.Value) ?? "User";
        var last = TrimOrNull(lastName) ?? "Account";

        return new User
        {
            Email = emailVo,
            PasswordHash = null,
            FirstName = first,
            LastName = last,
            PhoneNumber = string.Empty,
            Role = UserRole.User,
            IsEmailVerified = emailVerified,
            IsActive = true
        };
    }

    /// <summary>
    /// Links an enterprise IdP subject for a company. One active link per company per user (MVP).
    /// </summary>
    public CorporateSsoIdentityLink LinkCorporateSso(
        Guid companyId,
        SsoProtocol protocol,
        string subject,
        string idpIssuer,
        string? providerEmail = null,
        DateTime? linkedAtUtc = null)
    {
        if (CorporateSsoLinks.Any(l => l.CompanyId == companyId && !l.IsDeleted && !l.IsDisabled))
            throw new BusinessRuleException("User.LinkCorporateSso", "An active Corporate SSO link already exists for this company");

        var link = CorporateSsoIdentityLink.Create(companyId, Id, protocol, subject, idpIssuer, providerEmail, linkedAtUtc);
        CorporateSsoLinks.Add(link);
        UpdatedAt = DateTime.UtcNow;
        return link;
    }

    /// <summary>
    /// Links an external provider subject to this user. Enforces one link per provider per user in-memory;
    /// DB unique indexes remain the final guard.
    /// </summary>
    public UserExternalLogin LinkExternalLogin(
        ExternalAuthProvider provider,
        string providerSubject,
        string? providerEmail = null,
        DateTime? linkedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(providerSubject))
            throw new ValidationException("providerSubject", "Provider subject is required");

        if (ExternalLogins.Any(l => l.Provider == provider && !l.IsDeleted))
            throw new BusinessRuleException("User.LinkExternalLogin", $"Provider {provider} is already linked");

        var login = UserExternalLogin.Create(Id, provider, providerSubject, providerEmail, linkedAtUtc);
        ExternalLogins.Add(login);
        UpdatedAt = DateTime.UtcNow;
        return login;
    }

    public void UpdateProfile(string? firstName, string? lastName, string? phoneNumber)
    {
        if (!string.IsNullOrWhiteSpace(firstName))
            FirstName = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName))
            LastName = lastName.Trim();
        if (!string.IsNullOrWhiteSpace(phoneNumber))
            PhoneNumber = phoneNumber.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("passwordHash", "Password hash is required");
        PasswordHash = passwordHash;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RotateRefreshToken(string refreshToken, DateTime expiryUtc)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new ValidationException("refreshToken", "Refresh token is required");
        RefreshToken = refreshToken;
        RefreshTokenExpiryTime = expiryUtc;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Persist product-channel session bind (KD-2). Call after every successful mint.
    /// Company fields are only stored when <paramref name="channel"/> is Corporate; otherwise forced null.
    /// </summary>
    public void BindSession(ProductChannel channel, Guid? companyId = null, string? companyRole = null)
    {
        SessionChannel = channel;
        if (channel == ProductChannel.Corporate)
        {
            SessionCompanyId = companyId;
            SessionCompanyRole = companyRole;
        }
        else
        {
            SessionCompanyId = null;
            SessionCompanyRole = null;
        }
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears refresh token and session channel bind (KD-26).
    /// </summary>
    public void RevokeRefreshToken()
    {
        RefreshToken = null;
        RefreshTokenExpiryTime = null;
        SessionChannel = null;
        SessionCompanyId = null;
        SessionCompanyRole = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordLogin(string refreshToken, DateTime refreshTokenExpiryUtc)
    {
        if (!IsActive)
            throw new BusinessRuleException("User.RecordLogin", "Account is disabled");
        RotateRefreshToken(refreshToken, refreshTokenExpiryUtc);
        LastLoginAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        SetPasswordHash(newPasswordHash);
        RevokeRefreshToken();
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        RevokeRefreshToken();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkEmailVerified()
    {
        IsEmailVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPhoneVerified()
    {
        IsPhoneVerified = true;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    /// <summary>
    /// Prefer a readable local-part token as first name when IdP omits given name.
    /// Returns null when local-part is empty or not useful (e.g. pure digits only still allowed).
    /// </summary>
    private static string? DeriveFirstNameFromEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
            return null;

        var local = email[..at].Trim();
        if (string.IsNullOrEmpty(local))
            return null;

        // Strip common plus-tag and take first segment of dotted local-parts
        var plus = local.IndexOf('+');
        if (plus >= 0)
            local = local[..plus];

        var dot = local.IndexOf('.');
        if (dot > 0)
            local = local[..dot];

        local = local.Trim();
        if (string.IsNullOrEmpty(local))
            return null;

        // Capitalize first letter for display
        if (local.Length == 1)
            return local.ToUpperInvariant();

        return char.ToUpperInvariant(local[0]) + local[1..].ToLowerInvariant();
    }
}
