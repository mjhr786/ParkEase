using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingApp.API.Middleware;
using ParkingApp.API.Options;
using ParkingApp.Application.DTOs;
using ParkingApp.BuildingBlocks.Security;

namespace ParkingApp.UnitTests.API.Middleware;

/// <summary>
/// Theory tests iterate the same <see cref="ChannelRouteMatrix.Rules"/> collection the middleware uses (KD-5).
/// </summary>
public class ChannelAuthorizationMiddlewareTests
{
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static IEnumerable<object[]> AllRules()
    {
        foreach (var rule in ChannelRouteMatrix.Rules)
            yield return new object[] { rule.Id };
    }

    // ── Rule-table theory (same IReadOnlyList middleware uses) ───────────────

    [Theory]
    [MemberData(nameof(AllRules))]
    public void RuleTable_Rule_IsWellFormed(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        rule.PathPattern.Should().StartWith("/");
        rule.HttpMethod.Should().NotBeNullOrWhiteSpace();
        rule.Access.Should().NotBe(ChannelAccess.None);
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void RuleTable_SamplePath_IsMatchedByMatrix(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var samplePath = ChannelRouteMatrix.SamplePath(rule.PathPattern);
        var method = rule.HttpMethod == "*" ? "GET" : rule.HttpMethod;

        var match = ChannelRouteMatrix.FindMatch(method, samplePath);
        match.Should().NotBeNull(
            $"sample path {samplePath} for rule {id} must match some matrix row (first-match)");
        // First-match may be a more specific earlier rule; access must still be a subset of intended surface.
        // At minimum the matched rule must not be empty.
        match!.Access.Should().NotBe(ChannelAccess.None);
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Theory_IsAccessAllowed_Marketplace(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var user = User(ProductChannel.Marketplace, "User");
        var allowed = ChannelAuthorizationMiddleware.IsAccessAllowed(
            rule, ChannelAccess.Marketplace, isPlatformAdmin: false, user);

        allowed.Should().Be(rule.Access.HasFlag(ChannelAccess.Marketplace));
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Theory_IsAccessAllowed_CorporateBootstrap(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var user = User(ProductChannel.Corporate, "User");
        var allowed = ChannelAuthorizationMiddleware.IsAccessAllowed(
            rule, ChannelAccess.CorporateBootstrap, isPlatformAdmin: false, user);

        allowed.Should().Be(rule.Access.HasFlag(ChannelAccess.CorporateBootstrap));
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Theory_IsAccessAllowed_CorporateBound_Employee_RespectsCA(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var user = User(ProductChannel.Corporate, "User", CompanyA, "Employee");
        var allowed = ChannelAuthorizationMiddleware.IsAccessAllowed(
            rule, ChannelAccess.CorporateBound, isPlatformAdmin: false, user);

        var expected = rule.Access.HasFlag(ChannelAccess.CorporateBound) && !rule.RequireCompanyAdmin;
        allowed.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Theory_IsAccessAllowed_CorporateBound_CompanyAdmin(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var user = User(ProductChannel.Corporate, "User", CompanyA, "Admin");
        var allowed = ChannelAuthorizationMiddleware.IsAccessAllowed(
            rule, ChannelAccess.CorporateBound, isPlatformAdmin: false, user);

        allowed.Should().Be(rule.Access.HasFlag(ChannelAccess.CorporateBound));
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public void Theory_IsAccessAllowed_PlatformAdmin_OnA_Routes(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var user = User(ProductChannel.Marketplace, "Admin");
        var allowed = ChannelAuthorizationMiddleware.IsAccessAllowed(
            rule, ChannelAccess.Marketplace, isPlatformAdmin: true, user);

        var expected = rule.Access.HasFlag(ChannelAccess.PlatformAdminRole)
                       || rule.Access.HasFlag(ChannelAccess.Marketplace);
        allowed.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllRules))]
    public async Task Theory_Middleware_Marketplace_Path_Honors_MatchedRule(string id)
    {
        var rule = ChannelRouteMatrix.Rules.Single(r => r.Id == id);
        var samplePath = ChannelRouteMatrix.SamplePath(rule.PathPattern);
        var method = rule.HttpMethod == "*" ? "GET" : rule.HttpMethod;

        // What the matrix actually resolves for this sample (first match).
        var matched = ChannelRouteMatrix.FindMatch(method, samplePath);
        matched.Should().NotBeNull();

        var expectsAllow = ChannelAuthorizationMiddleware.IsAccessAllowed(
            matched!,
            ChannelAccess.Marketplace,
            isPlatformAdmin: false,
            User(ProductChannel.Marketplace, "User"));

        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, "User"),
            method: method,
            path: samplePath);

        if (expectsAllow)
        {
            nextCalled.Should().BeTrue($"Marketplace should pass matched rule {matched!.Id} (from {id})");
        }
        else
        {
            nextCalled.Should().BeFalse();
            await AssertChannelForbidden(context);
        }
    }

    // ── Acceptance scenarios ─────────────────────────────────────────────────

    [Fact]
    public async Task FlagDisabled_IsNoOp_EvenForUnmatchedPath()
    {
        var (context, nextCalled) = await InvokeAsync(
            enabled: false,
            user: User(ProductChannel.Marketplace, "User"),
            method: "GET",
            path: "/api/totally-unknown/endpoint");

        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Anonymous_Search_PassesMiddleware()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: Anonymous(),
            method: "GET",
            path: "/api/parking/search");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task AdminRole_MarketplaceChannel_AllowedOnAdminApi()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, role: "Admin"),
            method: "GET",
            path: "/api/admin/users");

        nextCalled.Should().BeTrue("KD-5a: Admin role fast-path on /api/admin/** regardless of channel");
    }

    [Fact]
    public async Task AdminRole_MarketplaceChannel_AllowedOnAdminOutbox()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, role: "Admin"),
            method: "GET",
            path: "/api/admin/outbox");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task MarketplaceUser_DeniedCompanyDashboard()
    {
        var path = $"/api/v1/corporate/companies/{CompanyA}/dashboard";
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, role: "User"),
            method: "GET",
            path: path);

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task UnmatchedAuthenticatedPath_Returns403_WithChannelForbiddenCode()
    {
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, role: "User"),
            method: "GET",
            path: "/api/unknown-surface/xyz");

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task MissingChannelClaim_TreatedAsMarketplace_DuringSoak()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "User"),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));

        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: user,
            method: "GET",
            path: "/api/dashboard/vendor");

        nextCalled.Should().BeTrue("missing claim soak → Marketplace allows dashboard");
    }

    [Fact]
    public async Task CorporateBound_MismatchedCompanyHeader_Denied()
    {
        var path = $"/api/v1/corporate/companies/{CompanyA}/dashboard";
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", CompanyA, "Admin"),
            method: "GET",
            path: path,
            companyHeader: CompanyB);

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task CorporateBound_MatchingCompany_AllowedOnDashboard()
    {
        var path = $"/api/v1/corporate/companies/{CompanyA}/dashboard";
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", CompanyA, "Admin"),
            method: "GET",
            path: path);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task CorporateEmployee_DeniedLeaseBrowseSearch()
    {
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", CompanyA, "Employee"),
            method: "GET",
            path: "/api/parking/search");

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task CorporateCompanyAdmin_AllowedLeaseBrowseSearch()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", CompanyA, "Admin"),
            method: "GET",
            path: "/api/parking/search");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Marketplace_VendorAllocations_Allowed()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, "User"),
            method: "GET",
            path: "/api/v1/corporate/vendor/allocations");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Corporate_VendorAllocations_Denied()
    {
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", CompanyA, "Admin"),
            method: "GET",
            path: "/api/v1/corporate/vendor/allocations");

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task Bootstrap_CreateCompany_Allowed()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", companyId: null),
            method: "POST",
            path: "/api/v1/corporate/companies");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Bootstrap_CompanyDashboard_Denied()
    {
        var path = $"/api/v1/corporate/companies/{CompanyA}/dashboard";
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Corporate, "User", companyId: null),
            method: "GET",
            path: path);

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public async Task NonApiPath_NotEnforced()
    {
        var (_, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, "User"),
            method: "GET",
            path: "/health");

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task User_WithoutAdminRole_DeniedAdminApi()
    {
        var (context, nextCalled) = await InvokeAsync(
            enabled: true,
            user: User(ProductChannel.Marketplace, "User"),
            method: "GET",
            path: "/api/admin/users");

        nextCalled.Should().BeFalse();
        await AssertChannelForbidden(context);
    }

    [Fact]
    public void PathMatches_SupportsGuidAndPrefix()
    {
        ChannelRouteMatrix.PathMatches(
                $"/api/v1/corporate/companies/{CompanyA}/dashboard",
                "/api/v1/corporate/companies/{companyId}/**")
            .Should().BeTrue();

        ChannelRouteMatrix.PathMatches("/api/parking/search", "/api/parking/search")
            .Should().BeTrue();

        ChannelRouteMatrix.PathMatches("/api/parking/map", "/api/parking/search")
            .Should().BeFalse();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(
        ProductChannel channel,
        string role,
        Guid? companyId = null,
        string? companyRole = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
            new(ParkEaseClaimTypes.Channel, channel.ToString()),
        };
        if (companyId.HasValue)
            claims.Add(new Claim(ParkEaseClaimTypes.CompanyId, companyId.Value.ToString()));
        if (!string.IsNullOrEmpty(companyRole))
            claims.Add(new Claim(ParkEaseClaimTypes.CompanyRole, companyRole));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeAsync(
        bool enabled,
        ClaimsPrincipal user,
        string method,
        string path,
        Guid? companyHeader = null)
    {
        var options = new ChannelIsolationOptions
        {
            Enabled = enabled,
            TreatMissingClaimAs = "Marketplace",
            EnforceCompanyClaimMatch = true,
            VendorAllocationAllowlistEnabled = true
        };

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new ChannelAuthorizationMiddleware(
            next,
            NullLogger<ChannelAuthorizationMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.User = user;
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (companyHeader.HasValue)
            context.Request.Headers["X-Company-Id"] = companyHeader.Value.ToString();

        var marker = "/api/v1/corporate/companies/";
        var idx = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = path[(idx + marker.Length)..];
            var segment = rest.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (Guid.TryParse(segment, out var cid))
                context.Request.RouteValues["companyId"] = cid.ToString();
        }

        await middleware.InvokeAsync(context, Options.Create(options));
        return (context, nextCalled);
    }

    private static async Task AssertChannelForbidden(HttpContext context)
    {
        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
        var body = JsonSerializer.Deserialize<ApiResponse<object>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be(ChannelAuthorizationMiddleware.ChannelForbiddenCode);
        body.Errors.Should().NotBeNull().And.Contain(ChannelAuthorizationMiddleware.ChannelForbiddenCode);
    }
}
