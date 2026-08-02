using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Application.CQRS;
using ParkingApp.Application.DTOs;
using ParkingApp.Corporate.Application.DTOs;
using ParkingApp.Domain.Enums;
using ParkingApp.Marketplace.Contracts.Enums;

namespace ParkingApp.IntegrationTests.Support;

/// <summary>
/// L4 HTTP smoke host: real pipeline + JWT auth scheme swapped for test auth.
/// IDispatcher is stubbed so dual-pool JSON contracts are verified without full DB migrate.
/// Channel isolation is disabled.
/// </summary>
public sealed class DualPoolApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    public static readonly Guid TestCompanyId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly Guid TestSpaceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    public static readonly Guid TestAllocationId = Guid.Parse("aaaaaaaa-0000-1111-2222-333333333333");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=5432;Database=parkease_http_smoke_unused;Username=test;Password=test",
                // Prefer in-memory cache (no Redis required)
                ["ConnectionStrings:Redis"] = "",
                ["Jwt:SecretKey"] = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
                ["Jwt:Issuer"] = "ParkingApp",
                ["Jwt:Audience"] = "ParkingApp",
                ["ChannelIsolation:Enabled"] = "false",
                ["Logging:File:Enabled"] = "false",
                ["Storage:Provider"] = "Local"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Avoid background DB/Redis work during HTTP smoke
            services.RemoveAll<IHostedService>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IDispatcher>();
            services.AddSingleton<IDispatcher, DualPoolDispatcherStub>();
        });
    }
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuth";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, DualPoolApiFactory.TestUserId.ToString()),
            new Claim(ClaimTypes.Email, "admin@dualpool.test"),
            new Claim("product_channel", "Corporate"),
            new Claim("company_id", DualPoolApiFactory.TestCompanyId.ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Returns dual-pool allocation payloads for corporate HTTP smoke paths.</summary>
internal sealed class DualPoolDispatcherStub : IDispatcher
{
    public Task<TResult> SendAsync<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
    {
        if (typeof(TResult) == typeof(ApiResponse<ParkingAllocationDto>))
        {
            var dto = SampleAllocation();
            object result = new ApiResponse<ParkingAllocationDto>(true, "ok", dto);
            return Task.FromResult((TResult)result);
        }

        throw new NotSupportedException($"SendAsync not stubbed for {typeof(TResult).Name} / {command.GetType().Name}");
    }

    public Task<TResult> QueryAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken = default)
    {
        if (typeof(TResult) == typeof(ApiResponse<List<ParkingAllocationDto>>))
        {
            object result = new ApiResponse<List<ParkingAllocationDto>>(
                true, null, new List<ParkingAllocationDto> { SampleAllocation() });
            return Task.FromResult((TResult)result);
        }

        throw new NotSupportedException($"QueryAsync not stubbed for {typeof(TResult).Name} / {query.GetType().Name}");
    }

    private static ParkingAllocationDto SampleAllocation() =>
        new(
            DualPoolApiFactory.TestAllocationId,
            DualPoolApiFactory.TestCompanyId,
            DualPoolApiFactory.TestSpaceId,
            "HQ Dual Lot",
            TotalSlots: 30,
            FixedSlots: 7,
            SharedSlots: 23,
            MonthlyRate: 0m,
            StartDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Status: AllocationStatus.Active,
            SourceType: ParkingAllocationSource.CompanyOwned,
            VendorId: null,
            LeaseReference: null,
            ApprovedByUserId: DualPoolApiFactory.TestUserId,
            ApprovedAt: DateTime.UtcNow,
            Policy: new BookingPolicyDto(10, 40, 1, TimeSpan.FromHours(0), TimeSpan.FromHours(23), true),
            FixedAssignments: new List<FixedSlotAssignmentDto>(),
            CreatedAt: DateTime.UtcNow,
            VendorName: null,
            TwoWheeler: new SlotPoolDto(10, 2, 8),
            FourWheeler: new SlotPoolDto(20, 5, 15));
}
