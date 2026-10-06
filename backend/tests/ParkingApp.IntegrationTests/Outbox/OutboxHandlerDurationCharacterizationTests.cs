using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using ParkingApp.Application.Interfaces;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.BuildingBlocks.Enums;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Infrastructure.Services;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Contracts.Enums;
using ParkingApp.Marketplace.Domain.Entities;
using ParkingApp.Marketplace.Domain.Events;
using ParkingApp.Notifications.Contracts;
using Xunit;
using Xunit.Abstractions;

namespace ParkingApp.IntegrationTests.Outbox;

/// <summary>
/// Test-only duration characterization of the frozen PR2 processor and the real
/// domain-event handlers. Email, SMS, and push providers are not called.
/// </summary>
[Collection(OutboxHandlerDurationCollection.Name)]
[Trait("Layer", "PostgreSQL")]
[Trait("Feature", "OutboxHandlerDuration")]
public sealed class OutboxHandlerDurationCharacterizationTests : IAsyncLifetime
{
    private const int TimedSamples = 20;
    private const int HandlerSamples = 8;

    private static readonly JsonSerializerOptions PayloadJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _connectionString;
    private readonly ITestOutputHelper _output;
    private readonly EfCommandObserver _commands = new();
    private readonly IDisposable _listenerSubscription;
    private OutboxHandlerDurationFactory? _factory;
    private SeededWorld? _world;
    private readonly string _resultsPath = Path.Combine(Path.GetTempPath(), "outbox-handler-duration-results.txt");

    public OutboxHandlerDurationCharacterizationTests(FullApiPostgresFixture postgres, ITestOutputHelper output)
    {
        _connectionString = postgres.ConnectionString;
        _output = output;
        _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(_commands);
    }

    public async Task InitializeAsync()
    {
        _factory = new OutboxHandlerDurationFactory(_connectionString);
        // Build the host (migrate against the throwaway database) before any sample.
        _ = _factory.Services;
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
            await _factory.DisposeAsync();
        _listenerSubscription.Dispose();
    }

    [Fact]
    public async Task Characterize_Dispatch_Claim_Terminal_And_PerHandler_Durations()
    {
        var factory = _factory ?? throw new InvalidOperationException("Factory was not started.");
        var world = _world ?? throw new InvalidOperationException("Seed was not created.");
        var lines = new List<string>();
        File.WriteAllText(_resultsPath, "");
        void Keep(string line)
        {
            lines.Add(line);
            File.AppendAllText(_resultsPath, line + Environment.NewLine);
            _output.WriteLine(line);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tokens = await db.DeviceTokens.CountAsync();
            tokens.Should().Be(0, "a device token would let the real Firebase sender run");
            scope.ServiceProvider.GetRequiredService<IEmailService>().Should().BeOfType<CountingEmailService>();
            scope.ServiceProvider.GetRequiredService<ISmsNotificationService>().Should().BeOfType<CountingSmsService>();
            scope.ServiceProvider.GetRequiredService<IPushNotificationService>().GetType().Name
                .Should().Contain("Firebase");
            scope.ServiceProvider.GetRequiredService<INotificationCoordinator>().GetType().Name
                .Should().Be("NotificationCoordinator");
            Keep("GUARD\ttokens=0\temail=CountingEmailService\tsms=CountingSmsService\tpush=Firebase\tcoordinator=NotificationCoordinator");
        }

        var discovered = DiscoverHandlerTypes();
        Keep("DISCOVERED\t" + string.Join(",", discovered.OrderBy(n => n, StringComparer.Ordinal)));
        discovered.Should().BeEquivalentTo(ExpectedHandlerTypeNames);

        var scenarios = BuildScenarios(world);
        var qrCreate = await TimeIsolatedTicketCreateAsync(world);
        Keep($"QR_CREATE\tn=1\tms={qrCreate:0.000}");

        foreach (var scenario in scenarios)
        {
            var handlers = ResolveHandlerNames(scenario.EventType);
            Keep("ORDER\t" + scenario.EventType.Name + "\t" + (handlers.Count == 0 ? "(none)" : string.Join(" > ", handlers)));
            handlers.Should().BeEquivalentTo(scenario.ExpectedHandlers, "handler registration drifted from the source inventory");

            var warmup = await RunSampleAsync(scenario, timed: false);
            Keep(FormatSample("WARMUP", scenario.EventType.Name, warmup));
            warmup.Status.Should().Be(OutboxStatus.Processed, warmup.LastError);

            var samples = new List<Sample>(TimedSamples);
            for (var i = 0; i < TimedSamples; i++)
                samples.Add(await RunSampleAsync(scenario, timed: true));

            samples.Should().OnlyContain(s => s.Status == OutboxStatus.Processed);
            samples.Should().OnlyContain(s => s.ClaimMs > 0 && s.TerminalMs > 0 && s.DispatchMs >= 0);
            if (scenario.MinimumEmails > 0)
                samples.Should().OnlyContain(s => s.Emails >= scenario.MinimumEmails);
            if (scenario.ExpectInbox)
                samples.Should().OnlyContain(s => s.InboxRows >= 1);
            else
                samples.Should().OnlyContain(s => s.InboxRows == 0);

            Keep(FormatStats("TYPE", scenario.EventType.Name, samples));
        }

        factory.Sms.Calls.Should().Be(0, "current handlers use Normal priority, so SMS send must not run");
        Keep($"SMS_CALLS\t{factory.Sms.Calls}");

        foreach (var scenario in scenarios.Where(s => s.ExpectedHandlers.Length > 0))
        {
            var names = ResolveHandlerNames(scenario.EventType);
            for (var index = 0; index < names.Count; index++)
            {
                var isolated = new List<double>(HandlerSamples);
                double? warmupMs = null;
                for (var n = 0; n < HandlerSamples + 1; n++)
                {
                    var ms = await TimeOneHandlerAsync(scenario, index);
                    if (n == 0)
                        warmupMs = ms;
                    else
                        isolated.Add(ms);
                }

                var stats = Stats.From(isolated);
                Keep(
                    "HANDLER\t" + names[index] +
                    "\tevent=" + scenario.EventType.Name +
                    "\twarmup=" + warmupMs!.Value.ToString("0.000") +
                    "\tn=" + stats.Count +
                    "\tmin=" + stats.Min.ToString("0.000") +
                    "\tp50=" + stats.P50.ToString("0.000") +
                    "\tmax=" + stats.Max.ToString("0.000") +
                    "\tavg=" + stats.Average.ToString("0.000") +
                    "\tp95=NOT_MEANINGFUL\tp99=NOT_MEANINGFUL");
            }
        }

    }

    private async Task<double> TimeIsolatedTicketCreateAsync(SeededWorld world)
    {
        var factory = _factory!;
        factory.Email.Reset();
        await using var scope = factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider
            .GetServices(typeof(IDomainEventHandler<BookingConfirmedEvent>))
            .Where(h => h is not null)
            .Cast<object>()
            .ToList();
        var ticket = handlers.Single(h => h.GetType().Name == "EventPackageTicketEmailHandler");
        var ev = new BookingConfirmedEvent(world.EmptyQrBookingId, world.MemberId, world.SpaceId, world.EmptyQrReference);
        var ms = await InvokeAsync(typeof(BookingConfirmedEvent), ticket, ev);
        factory.Email.Calls.Should().BeGreaterThan(0, "the empty-QR ticket path should reach the email stub");
        return ms;
    }

    private async Task<double> TimeOneHandlerAsync(Scenario scenario, int handlerIndex)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider
            .GetServices(typeof(IDomainEventHandler<>).MakeGenericType(scenario.EventType))
            .Where(h => h is not null)
            .Cast<object>()
            .ToList();
        return await InvokeAsync(scenario.EventType, handlers[handlerIndex], scenario.CreateEvent());
    }

    private static async Task<double> InvokeAsync(Type eventType, object handler, object domainEvent)
    {
        var method = typeof(IDomainEventHandler<>).MakeGenericType(eventType).GetMethod("HandleAsync")
            ?? throw new InvalidOperationException("HandleAsync missing");
        var sw = Stopwatch.StartNew();
        var task = (Task)method.Invoke(handler, new[] { domainEvent, CancellationToken.None })!;
        await task;
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private async Task<Sample> RunSampleAsync(Scenario scenario, bool timed)
    {
        var factory = _factory!;
        factory.Email.Reset();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

        await db.Notifications.IgnoreQueryFilters().ExecuteDeleteAsync();
        var beforeInbox = await db.Notifications.IgnoreQueryFilters().CountAsync();

        var ev = scenario.CreateEvent();
        var id = Guid.NewGuid();
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            TypeName = scenario.EventType.AssemblyQualifiedName ?? scenario.EventType.FullName!,
            Payload = JsonSerializer.Serialize(ev, scenario.EventType, PayloadJson),
            IdempotencyKey = "duration:" + scenario.EventType.Name + ":" + Guid.NewGuid().ToString("N"),
            Status = OutboxStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = DateTime.UtcNow,
            AvailableAfterUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        _commands.Begin();
        var wall = Stopwatch.StartNew();
        var processed = await processor.ProcessByIdsAsync(new[] { id });
        wall.Stop();
        var batch = _commands.End();

        var row = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
        var inbox = await db.Notifications.IgnoreQueryFilters().CountAsync() - beforeInbox;
        var claim = batch.LastOrDefault(c => c.Kind == CommandKind.Claim);
        var terminal = batch.LastOrDefault(c => c.Kind == CommandKind.Terminal);
        if (timed && (claim is null || terminal is null))
        {
            throw new InvalidOperationException(
                "Could not see claim and terminal commands for " + scenario.EventType.Name + Environment.NewLine +
                string.Join(Environment.NewLine + "---" + Environment.NewLine, batch.Select(c => c.Kind + " " + c.Sql)));
        }

        double dispatch = 0;
        if (claim is not null && terminal is not null)
            dispatch = Math.Max(0, (terminal.StartTimestamp - claim.EndTimestamp) * 1000.0 / Stopwatch.Frequency);

        return new Sample(
            processed,
            row.Status,
            row.LastError,
            wall.Elapsed.TotalMilliseconds,
            claim?.DurationMs ?? 0,
            terminal?.DurationMs ?? 0,
            dispatch,
            factory.Email.Calls,
            inbox);
    }

    private List<string> ResolveHandlerNames(Type eventType)
    {
        using var scope = _factory!.Services.CreateScope();
        return scope.ServiceProvider
            .GetServices(typeof(IDomainEventHandler<>).MakeGenericType(eventType))
            .Where(h => h is not null)
            .Select(h => h!.GetType().Name)
            .ToList();
    }

    private static HashSet<string> DiscoverHandlerTypes()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var assemblyName = assembly.GetName().Name ?? "";
            if (!assemblyName.StartsWith("ParkingApp", StringComparison.Ordinal)
                || assemblyName.Contains("Tests", StringComparison.Ordinal))
                continue;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types)
            {
                if (type is not { IsClass: true, IsAbstract: false })
                    continue;
                if (type.GetInterfaces().Any(i =>
                        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
                    names.Add(type.Name);
            }
        }

        return names;
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var owner = User.Register($"dur-owner-{suffix}@example.com", "hash", "Owner", "Duration", "9000000101");
        var member = User.Register($"dur-member-{suffix}@example.com", "hash", "Member", "Duration", "9000000102");
        db.Users.AddRange(owner, member);

        var space = new ParkingSpace
        {
            OwnerId = owner.Id,
            Title = "Duration Lot",
            Description = "Outbox handler duration characterization",
            Address = "1 Duration Road",
            City = "Dur" + suffix,
            State = "MH",
            Country = "IN",
            PostalCode = "411001",
            Latitude = 18.52,
            Longitude = 73.85,
            Location = new Point(73.85, 18.52) { SRID = 4326 },
            TotalSpots = 4,
            AvailableSpots = 4,
            HourlyRate = 100m,
            DailyRate = 800m,
            WeeklyRate = 4000m,
            MonthlyRate = 16000m,
            IsActive = true,
            IsDynamicPricingEnabled = true,
            TimeZoneId = "UTC"
        };
        db.ParkingSpaces.Add(space);
        await db.SaveChangesAsync();

        var start = DateTime.UtcNow.AddHours(1);
        var package = EventParkingPackage.Create(
            space.Id,
            owner.Id,
            "Duration Package",
            start.AddDays(1),
            start.AddDays(1).AddHours(3),
            packagePrice: 500m,
            totalSpots: 20,
            eventName: "Duration Event",
            venueName: "Duration Venue",
            zoneName: "A",
            earlyEntryMinutes: 30,
            lateExitMinutes: 30);
        db.Set<EventParkingPackage>().Add(package);

        Booking Make(string reference, Guid? packageId)
        {
            var booking = Booking.CreateMarketplace(
                member.Id,
                space.Id,
                start,
                start.AddHours(2),
                PricingType.Hourly,
                VehicleType.Car,
                baseAmount: 100m,
                taxAmount: 18m,
                serviceFee: 5m,
                discountAmount: 0m,
                totalAmount: 123m,
                vehicleNumber: "MH12AB1234",
                vehicleModel: "Duration",
                vehicleColor: "Blue",
                bookingReference: reference,
                eventParkingPackageId: packageId);
            booking.Status = BookingStatus.Confirmed;
            booking.ClearDomainEvents();
            return booking;
        }

        var standard = Make("DURSTD" + suffix, null);
        var eventBooking = Make("DUREVT" + suffix, package.Id);
        var emptyQr = Make("DURQR" + suffix, package.Id);
        emptyQr.QRCode = null;
        db.Bookings.AddRange(standard, eventBooking, emptyQr);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        _world = new SeededWorld(
            owner.Id,
            member.Id,
            space.Id,
            standard.Id,
            standard.BookingReference!,
            eventBooking.Id,
            eventBooking.BookingReference!,
            emptyQr.Id,
            emptyQr.BookingReference!);
    }

    private static List<Scenario> BuildScenarios(SeededWorld world)
    {
        var standard = (Func<object>)(() => new BookingRequestedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference));
        return
        [
            new(typeof(ParkingSpaceCreatedEvent), () => new ParkingSpaceCreatedEvent(world.SpaceId, world.OwnerId, "Duration Lot"), 0, false, ["ParkingSpaceCreatedCacheHandler"]),
            new(typeof(ParkingSpaceUpdatedEvent), () => new ParkingSpaceUpdatedEvent(world.SpaceId, "Duration Lot"), 0, false, ["ParkingSpaceUpdatedCacheHandler"]),
            new(typeof(ParkingSpaceDeletedEvent), () => new ParkingSpaceDeletedEvent(world.SpaceId, world.OwnerId), 0, false, ["ParkingSpaceDeletedCacheHandler"]),
            new(typeof(ParkingSpaceToggledEvent), () => new ParkingSpaceToggledEvent(world.SpaceId, true), 0, false, ["ParkingSpaceToggledCacheHandler"]),
            new(typeof(BookingRequestedEvent), standard, 2, true, ["BookingRequestedNotificationHandler", "BookingRequestedEmailHandler"]),
            new(typeof(BookingApprovedEvent), () => new BookingApprovedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, true), 1, true,
                ["BookingApprovedNotificationHandler", "BookingApprovedEmailHandler", "BookingApprovedParkingCacheHandler"]),
            new(typeof(BookingConfirmedEvent), () => new BookingConfirmedEvent(world.EventBookingId, world.MemberId, world.SpaceId, world.EventReference), 2, true,
                ["BookingConfirmedNotificationHandler", "BookingConfirmedEmailHandler", "EventPackageTicketEmailHandler", "BookingConfirmedParkingCacheHandler"]),
            new(typeof(BookingCancelledEvent), () => new BookingCancelledEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, "duration"), 0, true,
                ["BookingCancelledParkingCacheHandler", "BookingCancelledNotificationHandler"]),
            new(typeof(BookingRejectedEvent), () => new BookingRejectedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, "duration", world.OwnerId), 1, true,
                ["BookingRejectedNotificationHandler", "BookingRejectedParkingCacheHandler"]),
            new(typeof(BookingCheckedInEvent), () => new BookingCheckedInEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference), 0, true,
                ["BookingCheckedInParkingCacheHandler", "BookingCheckedInNotificationHandler", "BookingCheckedInGuestNotificationHandler"]),
            new(typeof(BookingCheckedOutEvent), () => new BookingCheckedOutEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference), 0, true,
                ["BookingCheckedOutParkingCacheHandler", "BookingCheckedOutGuestNotificationHandler"]),
            new(typeof(BookingSessionEndRemindedEvent), () => new BookingSessionEndRemindedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference), 0, true,
                ["BackgroundJobNotificationHandlers"]),
            new(typeof(BookingOverstayNotifiedEvent), () => new BookingOverstayNotifiedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference), 0, true,
                ["BackgroundJobNotificationHandlers"]),
            new(typeof(BookingOverstayFeeAssessedEvent), () => new BookingOverstayFeeAssessedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, 25m, 10, 25m), 0, true,
                ["BackgroundJobNotificationHandlers"]),
            new(typeof(BookingAutoCheckedOutEvent), () => new BookingAutoCheckedOutEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, 25m), 0, true,
                ["BackgroundJobNotificationHandlers"]),
            new(typeof(BookingOverstayFeePaidEvent), () => new BookingOverstayFeePaidEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, 25m, 0m, "txn"), 0, false, []),
            new(typeof(BookingExtensionRequestedEvent), () => new BookingExtensionRequestedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, DateTime.UtcNow.AddHours(4), 40m), 2, true,
                ["BookingExtensionRequestedNotificationHandler"]),
            new(typeof(BookingExtensionApprovedEvent), () => new BookingExtensionApprovedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, true, 40m, DateTime.UtcNow.AddHours(4), world.OwnerId), 1, true,
                ["BookingExtensionApprovedNotificationHandler"]),
            new(typeof(BookingExtensionRejectedEvent), () => new BookingExtensionRejectedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, "duration", world.OwnerId), 1, true,
                ["BookingExtensionRejectedNotificationHandler"]),
            new(typeof(BookingExtensionConfirmedEvent), () => new BookingExtensionConfirmedEvent(world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, DateTime.UtcNow.AddHours(4), 40m), 2, true,
                ["BookingExtensionConfirmedNotificationHandler"]),
            new(typeof(PaymentCompletedEvent), () => new PaymentCompletedEvent(Guid.NewGuid(), world.StandardBookingId, world.MemberId, world.SpaceId, world.StandardReference, 123m, "INR", false), 2, true,
                ["PaymentCompletedNotificationHandler"])
        ];
    }

    private static string FormatSample(string kind, string name, Sample sample) =>
        kind + "\t" + name +
        "\ttotal=" + sample.TotalMs.ToString("0.000") +
        "\tclaim=" + sample.ClaimMs.ToString("0.000") +
        "\tdispatch=" + sample.DispatchMs.ToString("0.000") +
        "\tterminal=" + sample.TerminalMs.ToString("0.000") +
        "\temails=" + sample.Emails +
        "\tinbox=" + sample.InboxRows +
        "\tstatus=" + sample.Status +
        "\terror=" + (sample.LastError ?? "");

    private static string FormatStats(string kind, string name, IReadOnlyList<Sample> samples)
    {
        var total = Stats.From(samples.Select(s => s.TotalMs));
        var claim = Stats.From(samples.Select(s => s.ClaimMs));
        var dispatch = Stats.From(samples.Select(s => s.DispatchMs));
        var terminal = Stats.From(samples.Select(s => s.TerminalMs));
        return kind + "\t" + name +
               "\tn=" + total.Count +
               "\ttotalMin=" + total.Min.ToString("0.000") +
               "\ttotalP50=" + total.P50.ToString("0.000") +
               "\ttotalMax=" + total.Max.ToString("0.000") +
               "\ttotalAvg=" + total.Average.ToString("0.000") +
               "\tclaimMin=" + claim.Min.ToString("0.000") +
               "\tclaimP50=" + claim.P50.ToString("0.000") +
               "\tclaimMax=" + claim.Max.ToString("0.000") +
               "\tdispatchMin=" + dispatch.Min.ToString("0.000") +
               "\tdispatchP50=" + dispatch.P50.ToString("0.000") +
               "\tdispatchMax=" + dispatch.Max.ToString("0.000") +
               "\tdispatchAvg=" + dispatch.Average.ToString("0.000") +
               "\tterminalMin=" + terminal.Min.ToString("0.000") +
               "\tterminalP50=" + terminal.P50.ToString("0.000") +
               "\tterminalMax=" + terminal.Max.ToString("0.000") +
               "\tp95=NOT_MEANINGFUL\tp99=NOT_MEANINGFUL";
    }

    private static readonly string[] ExpectedHandlerTypeNames =
    [
        "ParkingSpaceCreatedCacheHandler",
        "ParkingSpaceUpdatedCacheHandler",
        "ParkingSpaceDeletedCacheHandler",
        "ParkingSpaceToggledCacheHandler",
        "BookingConfirmedParkingCacheHandler",
        "BookingCancelledParkingCacheHandler",
        "BookingApprovedParkingCacheHandler",
        "BookingRejectedParkingCacheHandler",
        "BookingCheckedInParkingCacheHandler",
        "BookingCheckedOutParkingCacheHandler",
        "BookingCheckedInNotificationHandler",
        "BookingCancelledNotificationHandler",
        "EventPackageTicketEmailHandler",
        "BackgroundJobNotificationHandlers",
        "BookingRequestedNotificationHandler",
        "BookingApprovedNotificationHandler",
        "BookingConfirmedNotificationHandler",
        "BookingRejectedNotificationHandler",
        "BookingRequestedEmailHandler",
        "BookingApprovedEmailHandler",
        "BookingConfirmedEmailHandler",
        "BookingExtensionRequestedNotificationHandler",
        "BookingExtensionApprovedNotificationHandler",
        "BookingExtensionRejectedNotificationHandler",
        "BookingExtensionConfirmedNotificationHandler",
        "PaymentCompletedNotificationHandler",
        "BookingCheckedInGuestNotificationHandler",
        "BookingCheckedOutGuestNotificationHandler"
    ];

    private sealed record SeededWorld(
        Guid OwnerId,
        Guid MemberId,
        Guid SpaceId,
        Guid StandardBookingId,
        string StandardReference,
        Guid EventBookingId,
        string EventReference,
        Guid EmptyQrBookingId,
        string EmptyQrReference);

    private sealed record Scenario(
        Type EventType,
        Func<object> CreateEvent,
        int MinimumEmails,
        bool ExpectInbox,
        string[] ExpectedHandlers);

    private sealed record Sample(
        int ProcessedCount,
        OutboxStatus Status,
        string? LastError,
        double TotalMs,
        double ClaimMs,
        double TerminalMs,
        double DispatchMs,
        int Emails,
        int InboxRows);

    private readonly record struct Stats(int Count, double Min, double P50, double Max, double Average)
    {
        public static Stats From(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToArray();
            if (sorted.Length == 0)
                return new Stats(0, 0, 0, 0, 0);
            var mid = sorted.Length / 2;
            var p50 = sorted.Length % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2;
            return new Stats(sorted.Length, sorted[0], p50, sorted[^1], sorted.Average());
        }
    }
}

[CollectionDefinition(OutboxHandlerDurationCollection.Name)]
public sealed class OutboxHandlerDurationCollection : ICollectionFixture<FullApiPostgresFixture>
{
    public const string Name = "OutboxHandlerDuration";
}

internal sealed class OutboxHandlerDurationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public OutboxHandlerDurationFactory(string connectionString)
    {
        _connectionString = connectionString;
        Email = new CountingEmailService();
        Sms = new CountingSmsService();
    }

    public CountingEmailService Email { get; }
    public CountingSmsService Sms { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Development);
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Logging:File:Enabled", "false");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["ConnectionStrings:Redis"] = "",
                ["Jwt:SecretKey"] = FullApiFactory.JwtSecret,
                ["Jwt:Issuer"] = FullApiFactory.JwtIssuer,
                ["Jwt:Audience"] = FullApiFactory.JwtAudience,
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Logging:File:Enabled"] = "false",
                ["Logging:Serilog:MinimumLevel"] = "Warning",
                ["Storage:Provider"] = "Local",
                ["RateLimiting:Disabled"] = "true",
                ["ExternalAuth:Enabled"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            TestDbContextRegistration.ReplacePostgres(services, _connectionString);
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);
            services.RemoveAll<ISmsNotificationService>();
            services.AddSingleton<ISmsNotificationService>(Sms);
        });
    }
}

internal sealed class CountingEmailService : IEmailService
{
    private int _calls;
    public int Calls => _calls;
    public void Reset() => Interlocked.Exchange(ref _calls, 0);

    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        Interlocked.Increment(ref _calls);
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(
        string to,
        string subject,
        string body,
        IReadOnlyList<EmailAttachment>? attachments,
        bool isHtml = true)
    {
        Interlocked.Increment(ref _calls);
        return Task.CompletedTask;
    }
}

internal sealed class CountingSmsService : ISmsNotificationService
{
    private int _calls;
    public int Calls => _calls;

    public Task<SmsResult> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(new SmsResult(true, "not-sent"));
    }

    public Task<IEnumerable<SmsResult>> SendBulkAsync(
        IEnumerable<string> phoneNumbers,
        string message,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult<IEnumerable<SmsResult>>(Array.Empty<SmsResult>());
    }

    public Task<SmsResult> SendTemplatedAsync(
        string phoneNumber,
        string templateId,
        Dictionary<string, string> placeholders,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(new SmsResult(true, "not-sent"));
    }
}

internal enum CommandKind
{
    Other,
    Claim,
    Terminal
}

internal sealed class ObservedCommand
{
    public CommandKind Kind { get; init; }
    public string Sql { get; init; } = "";
    public double DurationMs { get; init; }
    public long StartTimestamp { get; init; }
    public long EndTimestamp { get; init; }
}

internal sealed class EfCommandObserver : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
{
    private readonly object _gate = new();
    private List<ObservedCommand>? _current;

    public void Begin()
    {
        lock (_gate)
            _current = new List<ObservedCommand>();
    }

    public IReadOnlyList<ObservedCommand> End()
    {
        lock (_gate)
        {
            var batch = _current ?? new List<ObservedCommand>();
            _current = null;
            return batch;
        }
    }

    public void OnNext(DiagnosticListener value)
    {
        if (value.Name == "Microsoft.EntityFrameworkCore")
            value.Subscribe(this);
    }

    public void OnNext(KeyValuePair<string, object?> value)
    {
        if (value.Key != "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted")
            return;
        if (value.Value is not CommandExecutedEventData data)
            return;

        List<ObservedCommand>? batch;
        lock (_gate)
            batch = _current;
        if (batch is null)
            return;

        var end = Stopwatch.GetTimestamp();
        var durationTicks = (long)(data.Duration.TotalSeconds * Stopwatch.Frequency);
        var sql = data.Command.CommandText ?? "";
        var kind = CommandKind.Other;
        if (sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase))
        {
            // The terminal fence also names AttemptCount in its WHERE clause.
            // Only the claim statement increments it.
            if (sql.Contains("ProcessedAtUtc", StringComparison.OrdinalIgnoreCase)
                || sql.Contains("\"LastError\"", StringComparison.Ordinal))
                kind = CommandKind.Terminal;
            else if (sql.Contains("\"AttemptCount\" =", StringComparison.Ordinal)
                     && sql.Contains("+ 1", StringComparison.Ordinal))
                kind = CommandKind.Claim;
        }

        var observed = new ObservedCommand
        {
            Kind = kind,
            Sql = sql,
            DurationMs = data.Duration.TotalMilliseconds,
            EndTimestamp = end,
            StartTimestamp = end - Math.Max(0, durationTicks)
        };
        lock (_gate)
            batch.Add(observed);
    }

    public void OnError(Exception error)
    {
    }

    public void OnCompleted()
    {
    }
}
