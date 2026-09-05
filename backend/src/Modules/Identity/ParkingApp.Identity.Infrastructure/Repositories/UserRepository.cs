using Microsoft.EntityFrameworkCore;
using ParkingApp.BuildingBlocks.ValueObjects;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Identity.Domain.Enums;
using ParkingApp.Identity.Domain.Interfaces;
using ParkingApp.Identity.Infrastructure.Persistence;

namespace ParkingApp.Identity.Infrastructure.Repositories;

internal class UserRepository : IdentityRepository<User>, IUserRepository
{
    public UserRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = new Email(email);
            return await _dbSet.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<User?> GetByEmailIncludingDeletedAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalized = new Email(email);
            // Soft-deleted rows remain in IX_Users_Email; collision checks must see them.
            return await _dbSet
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);

    public async Task<User?> GetByExternalLoginAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerSubject))
            return null;

        var subject = providerSubject.Trim();
        return await _dbSet
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(
                u => u.ExternalLogins.Any(l => l.Provider == provider && l.ProviderSubject == subject),
                cancellationToken);
    }

    public async Task<(IReadOnlyList<User> Items, int TotalCount)> SearchForAdminAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsQueryable();

        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(term)
                || u.LastName.ToLower().Contains(term)
                || u.PhoneNumber.ToLower().Contains(term)
                || EF.Property<string>(u, nameof(User.Email)).ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}

internal class UserExternalLoginRepository : IdentityRepository<UserExternalLogin>, IUserExternalLoginRepository
{
    public UserExternalLoginRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<UserExternalLogin?> GetByProviderSubjectAsync(
        ExternalAuthProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerSubject))
            return null;

        var subject = providerSubject.Trim();
        return await _dbSet.FirstOrDefaultAsync(
            l => l.Provider == provider && l.ProviderSubject == subject,
            cancellationToken);
    }

    public async Task<IReadOnlyList<UserExternalLogin>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Provider)
            .ToListAsync(cancellationToken);
    }
}

internal class CorporateSsoIdentityLinkRepository : IdentityRepository<CorporateSsoIdentityLink>, ICorporateSsoIdentityLinkRepository
{
    public CorporateSsoIdentityLinkRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<CorporateSsoIdentityLink?> GetByCompanySubjectAsync(
        Guid companyId,
        SsoProtocol protocol,
        string subject,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var sub = subject.Trim();
        return await _dbSet.FirstOrDefaultAsync(
            l => l.CompanyId == companyId && l.Protocol == protocol && l.Subject == sub,
            cancellationToken);
    }

    public async Task<CorporateSsoIdentityLink?> GetByCompanyUserAsync(
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(
            l => l.CompanyId == companyId && l.UserId == userId,
            cancellationToken);

    public async Task<IReadOnlyList<CorporateSsoIdentityLink>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LinkedAtUtc)
            .ToListAsync(cancellationToken);
}

internal class VehicleRepository : IdentityRepository<Vehicle>, IVehicleRepository
{
    public VehicleRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet
            .Where(v => v.UserId == userId && !v.IsDeleted)
            .OrderByDescending(v => v.IsDefault)
            .ThenByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<Vehicle?> GetDefaultVehicleAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(v => v.UserId == userId && v.IsDefault && !v.IsDeleted, cancellationToken);
}

internal class DeviceTokenRepository : IdentityRepository<DeviceToken>, IDeviceTokenRepository
{
    public DeviceTokenRepository(IIdentityDbContext context) : base((DbContext)context) { }

    public async Task<IEnumerable<DeviceToken>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(d => d.UserId == userId).ToListAsync(cancellationToken);

    public async Task<DeviceToken?> GetByDeviceIdAndUserIdAsync(string deviceId, Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.FirstOrDefaultAsync(d => d.DeviceId == deviceId && d.UserId == userId, cancellationToken);

    public async Task<IEnumerable<string>> GetFcmTokensByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbSet.Where(d => d.UserId == userId).Select(d => d.FcmToken).ToListAsync(cancellationToken);
}
