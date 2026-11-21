using System.Data.Common;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Domain.Events;
using Xunit.Abstractions;

namespace ParkingApp.IntegrationTests.Outbox;

/// <summary>
/// Measures one frozen-PR2 claim when PostgreSQL commits and the success response
/// is not delivered. The client has to hit its own command timeout. This file does
/// not change the processor, the retry settings, or the claim SQL.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxTimeoutLossCollection : ICollectionFixture<FullApiPostgresFixture>
{
    public const string Name = "OutboxTimeoutLoss";
}

[Collection(OutboxTimeoutLossCollection.Name)]
[Trait("Layer", "PostgreSQL")]
[Trait("Feature", "OutboxTimeoutLoss")]
public sealed class OutboxTimeoutLostResponseCharacterizationTests
{
    private const int Runs = 2;

    private static readonly SemaphoreSlim MigrateGate = new(1, 1);
    private static string? _migratedConnection;

    private readonly string _direct;
    private readonly ITestOutputHelper _output;

    public OutboxTimeoutLostResponseCharacterizationTests(FullApiPostgresFixture postgres, ITestOutputHelper output)
    {
        _direct = postgres.ConnectionString;
        _output = output;
    }

    [Fact(Timeout = 240000)]
    public async Task TimeoutShaped_CommittedClaim_ClientWaitsForCommandTimeout()
    {
        var runs = new List<RunResult>(Runs);
        for (var i = 1; i <= Runs; i++)
            runs.Add(await RunAsync(i));

        var summary = BuildSummary(runs);
        _output.WriteLine(summary);
        var summaryPath = Path.Combine(Path.GetTempPath(), "outbox-timeout-loss-summary.txt");
        await File.WriteAllTextAsync(summaryPath, summary);

        foreach (var result in runs)
        {
            result.ClaimCommandTimeouts.Should().NotBeEmpty(result.Trace);
            result.ClaimCommandTimeouts.Should().OnlyContain(timeout => timeout == 30, result.Trace);
            result.Withheld.Should().NotBeNull(result.Trace);
            result.Withheld!.ReadyStatus.Should().Be("I", result.Trace);
            result.Withheld.CommandTags.Should().Contain("UPDATE 1", result.Trace);
            result.Withheld.ObservedRowCount.Should().Be(1, result.Trace);
            result.Withheld.ObservedStatus.Should().Be((int)OutboxStatus.Processing, result.Trace);
            result.Withheld.ObservedAttempt.Should().Be(result.BeforeAttempt + 1, result.Trace);
            result.Withheld.ObserverError.Should().BeNull(result.Trace);
            result.Cycles.Should().NotContain(
                cycle => cycle.Action == "ForwardedClaim" && cycle.CommandTags.Contains("UPDATE 1", StringComparison.Ordinal),
                result.Trace);
            result.Marks.Should().NotContain(mark => mark.Name == "HoldExpired", result.Trace);
            result.CommitObservedMs.Should().NotBeNull(result.Trace);
            result.FirstClaimFailedMs.Should().NotBeNull(result.Trace);
            result.FirstClaimStartedMs.Should().NotBeNull(result.Trace);
            var waited = result.FirstClaimFailedMs!.Value - result.FirstClaimStartedMs!.Value;
            waited.Should().BeInRange(28_000, 45_000, result.Trace);
            (result.FirstClaimFailedMs.Value - result.CommitObservedMs!.Value).Should().BeGreaterThan(25_000, result.Trace);
            result.CommitObservedMs.Value.Should().BeLessThan(result.FirstClaimStartedMs.Value + 5_000, result.Trace);
            result.ClientEvents.Should().NotContain(
                e => e.Claim && e.Phase == "NonQueryExecuted" && e.Detail.Contains("rows=1", StringComparison.Ordinal),
                result.Trace);
            result.HandlerCalls.Should().Be(0, result.Trace);
            result.ProcessedReturn.Should().NotBe(1, result.Trace);
            result.AfterStatus.Should().Be(OutboxStatus.Processing, result.Trace);
            result.AfterAttempt.Should().Be(result.Withheld.ObservedAttempt, result.Trace);
            result.Cycles.Should().NotContain(cycle =>
                cycle.CommandTags.Contains("BEGIN", StringComparison.OrdinalIgnoreCase)
                || cycle.CommandTags.Contains("COMMIT", StringComparison.OrdinalIgnoreCase)
                || cycle.CommandTags.Contains("ROLLBACK", StringComparison.OrdinalIgnoreCase),
                result.Trace);
        }
    }

    private async Task<RunResult> RunAsync(int run)
    {
        await EnsureMigratedAsync();
        var id = await SeedAsync();
        var before = await ReloadAsync(id);
        var upstream = new NpgsqlConnectionStringBuilder(_direct);
        if (string.IsNullOrWhiteSpace(upstream.Host))
            throw new InvalidOperationException("Testcontainer connection string has no host.");

        var origin = Stopwatch.GetTimestamp();
        long Elapsed() => (long)Stopwatch.GetElapsedTime(origin).TotalMilliseconds;
        await using var proxy = await OutboxTimeoutLossProxy.StartAsync(
            upstream.Host,
            upstream.Port,
            _direct,
            Elapsed);
        var tracer = new ClaimTraceInterceptor(Elapsed);
        await using var db = CreateProcessorContext(Proxied(_direct, proxy.Port), tracer);
        var strategy = db.Database.CreateExecutionStrategy();
        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        await using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);

        int? processed = null;
        Exception? error = null;
        try
        {
            processed = await processor.ProcessByIdsAsync(new[] { id });
        }
        catch (Exception ex)
        {
            error = ex;
        }

        var elapsed = Elapsed();
        var after = await ReloadAsync(id);
        var marks = proxy.Marks.ToList();
        var events = tracer.Events.ToList();
        var result = new RunResult(
            run,
            DescribeEnvironment(proxy.Port),
            DescribeStrategy(strategy),
            DescribeClientSettings(Proxied(_direct, proxy.Port)),
            before.Status,
            before.AttemptCount,
            after.Status,
            after.AttemptCount,
            processed,
            handler.Calls,
            error,
            elapsed,
            proxy.Cycles.ToList(),
            marks,
            events,
            tracer.ClaimCommandTimeouts.ToList(),
            FirstMs(marks, "CommitObserved"),
            FirstClaim(events, "NonQueryExecuting"),
            FirstClaim(events, "CommandFailed"),
            proxy.FormatTrace() + Environment.NewLine + "CLIENT" + Environment.NewLine + FormatEvents(events));
        _output.WriteLine($"RUN {run} finished elapsedMs={elapsed} return={processed?.ToString() ?? "threw"} handler={handler.Calls}");
        return result;
    }

    private static string BuildSummary(IReadOnlyList<RunResult> runs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TIMEOUT-SHAPED LOST RESPONSE");
        foreach (var result in runs)
        {
            var retryStart = result.ClientEvents
                .Where(e => e.Claim && e.Phase == "NonQueryExecuting")
                .Skip(1)
                .Select(e => (long?)e.ElapsedMs)
                .FirstOrDefault();
            var retryForward = result.Marks
                .Where(m => m.Name == "ClaimForwardedToServer")
                .Skip(1)
                .Select(m => (long?)m.ElapsedMs)
                .FirstOrDefault();
            var responseRead = result.Marks
                .Where(m => m.Name == "ClaimResponseRead")
                .Select(m => (long?)m.ElapsedMs)
                .ToList();
            var forwarded = result.Marks
                .Where(m => m.Name == "ClaimForwardedToServer")
                .Select(m => m.ElapsedMs)
                .ToList();
            var zeroRows = result.ClientEvents
                .Where(e => e.Claim && e.Phase == "NonQueryExecuted")
                .Select(e => $"{e.ElapsedMs}ms {e.Detail}")
                .ToList();
            var claimFailures = result.ClientEvents
                .Where(e => e.Claim && e.Phase == "CommandFailed")
                .Select(e => $"{e.ElapsedMs}ms {e.Detail}")
                .ToList();
            long? commitToRetryClient = retryStart.HasValue && responseRead.Count > 0
                ? retryStart.Value - responseRead[0]
                : null;
            long? observedToRetry = retryStart.HasValue && result.CommitObservedMs.HasValue
                ? retryStart.Value - result.CommitObservedMs.Value
                : null;
            long? forwardToRetry = retryStart.HasValue && forwarded.Count > 0
                ? retryStart.Value - forwarded[0]
                : null;

            builder.AppendLine($"RUN {result.Run}");
            builder.AppendLine(result.Environment);
            builder.AppendLine(result.Strategy);
            builder.AppendLine(result.ClientSettings);
            builder.AppendLine($"beforeStatus={(int)result.BeforeStatus} beforeAttempt={result.BeforeAttempt}");
            builder.AppendLine($"afterStatus={(int)result.AfterStatus} afterAttempt={result.AfterAttempt}");
            builder.AppendLine($"processedReturn={(result.ProcessedReturn?.ToString() ?? "threw")} handlerCalls={result.HandlerCalls} totalElapsedMs={result.TotalElapsedMs}");
            builder.AppendLine("exception=" + (result.Error == null ? "none" : DescribeException(result.Error)));
            builder.AppendLine($"withheldTags={result.Withheld?.CommandTags ?? "-"} withheldReady={result.Withheld?.ReadyStatus ?? "-"} observedStatus={result.Withheld?.ObservedStatus?.ToString() ?? "-"} observedAttempt={result.Withheld?.ObservedAttempt?.ToString() ?? "-"}");
            builder.AppendLine($"T0_claimExecutingMs={Fmt(result.FirstClaimStartedMs)}");
            builder.AppendLine($"T1_claimForwardedMs={Fmt(forwarded.Count > 0 ? forwarded[0] : null)}");
            builder.AppendLine($"T3_readyForQueryReadMs={Fmt(responseRead.Count > 0 ? responseRead[0] : null)}");
            builder.AppendLine($"T_commitObservedMs={Fmt(result.CommitObservedMs)}");
            builder.AppendLine($"T5_commandFailedMs={Fmt(result.FirstClaimFailedMs)}");
            builder.AppendLine($"T6_retryExecutingMs={Fmt(retryStart)}");
            builder.AppendLine($"T7_retryForwardedMs={Fmt(retryForward)}");
            builder.AppendLine($"T8_clientRows={string.Join(" | ", zeroRows)}");
            builder.AppendLine($"T9_processReturnMs={result.TotalElapsedMs}");
            builder.AppendLine($"waitedMs={(result.FirstClaimFailedMs.HasValue && result.FirstClaimStartedMs.HasValue ? result.FirstClaimFailedMs.Value - result.FirstClaimStartedMs.Value : -1)}");
            builder.AppendLine($"commitObservedToRetryMs={Fmt(observedToRetry)}");
            builder.AppendLine($"readyReadToRetryMs={Fmt(commitToRetryClient)}");
            builder.AppendLine($"forwardedToRetryMs={Fmt(forwardToRetry)}");
            builder.AppendLine($"claimExecutions={result.ClientEvents.Count(e => e.Claim && e.Phase == "NonQueryExecuting")}");
            builder.AppendLine($"claimFailures={result.ClientEvents.Count(e => e.Claim && e.Phase == "CommandFailed")}");
            builder.AppendLine("claimFailureDetail=" + string.Join(" || ", claimFailures));
            builder.AppendLine("responseReads=" + string.Join(" | ", responseRead));
            builder.AppendLine("cancelForwarded=" + result.Marks.Any(m => m.Name == "CancelRequestForwarded"));
            builder.AppendLine("lateServer=" + string.Join(" | ", result.Marks.Where(m => m.Name == "LateServerMessage").Select(m => m.ElapsedMs + "ms " + m.Detail)));
            builder.AppendLine("MARKS");
            foreach (var mark in result.Marks)
                builder.AppendLine($"  {mark.ElapsedMs} {mark.Name} conn={mark.ConnectionId} {mark.Detail}");
            builder.AppendLine("CYCLES");
            foreach (var cycle in result.Cycles)
                builder.AppendLine($"  {cycle.ElapsedMs} {cycle.Action} ready={cycle.ReadyStatus} tags=[{cycle.CommandTags}] status={cycle.ObservedStatus} attempt={cycle.ObservedAttempt}");
        }

        return builder.ToString();
    }

    private static string Fmt(long? value) => value?.ToString() ?? "unobserved";

    private static long? FirstMs(IReadOnlyList<OutboxTimeoutLossProxy.Mark> marks, string name) =>
        marks.Where(mark => mark.Name == name).Select(mark => (long?)mark.ElapsedMs).FirstOrDefault();

    private static long? FirstClaim(IReadOnlyList<ClientEvent> events, string phase) =>
        events.Where(e => e.Claim && e.Phase == phase).Select(e => (long?)e.ElapsedMs).FirstOrDefault();

    private static string FormatEvents(IReadOnlyList<ClientEvent> events)
    {
        var builder = new StringBuilder();
        foreach (var e in events)
            builder.Append("utc-elapsed=").Append(e.ElapsedMs).Append("ms ").Append(e.Phase)
                .Append(" claim=").Append(e.Claim)
                .Append(" commandTimeout=").Append(e.CommandTimeout)
                .Append("s ").Append(e.Detail)
                .Append(" SQL=").AppendLine(e.Sql);
        return builder.ToString();
    }

    private async Task EnsureMigratedAsync()
    {
        await MigrateGate.WaitAsync();
        try
        {
            if (_migratedConnection == _direct)
                return;

            await using var db = CreateDirect(commandTimeout: 120, retry: false);
            await db.Database.MigrateAsync();
            _migratedConnection = _direct;
        }
        finally
        {
            MigrateGate.Release();
        }
    }

    private async Task<Guid> SeedAsync()
    {
        await using var db = CreateDirect(commandTimeout: 30, retry: false);
        await db.OutboxMessages.ExecuteDeleteAsync();
        var writer = new OutboxWriter(db);
        writer.Enqueue(new BookingCancelledEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "BK",
            "timeout-loss"));
        await db.SaveChangesAsync();
        return writer.TakeEnqueuedMessageIds().Single();
    }

    private async Task<OutboxMessage> ReloadAsync(Guid id)
    {
        await using var db = CreateDirect(commandTimeout: 30, retry: false);
        return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    private ApplicationDbContext CreateProcessorContext(string connectionString, DbCommandInterceptor tracer)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.CommandTimeout(30);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
            })
            .ConfigureWarnings(IgnoreContextWarnings)
            .AddInterceptors(tracer);
        var context = new ApplicationDbContext(builder.Options);
        context.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        return context;
    }

    private ApplicationDbContext CreateDirect(int commandTimeout, bool retry)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_direct, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.CommandTimeout(commandTimeout);
                if (retry)
                    npgsql.EnableRetryOnFailure(maxRetryCount: 3);
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
            })
            .ConfigureWarnings(IgnoreContextWarnings);
        return new ApplicationDbContext(builder.Options);
    }

    private string DescribeEnvironment(int proxyPort)
    {
        var upstream = new NpgsqlConnectionStringBuilder(_direct);
        string serverVersion;
        using (var connection = new NpgsqlConnection(_direct))
        {
            connection.Open();
            using var command = new NpgsqlCommand("SELECT version()", connection);
            serverVersion = (string)command.ExecuteScalar()!;
        }

        var ef = typeof(DbContext).Assembly.GetName();
        var npgsql = typeof(NpgsqlConnection).Assembly.GetName();
        return string.Join(Environment.NewLine, new[]
        {
            $"runtime={Environment.Version}",
            $"ef={ef.Name} {ef.Version}",
            $"npgsql={npgsql.Name} {npgsql.Version}",
            $"postgres={serverVersion}",
            $"upstreamHost={upstream.Host} upstreamPort={upstream.Port} database={upstream.Database} proxyPort={proxyPort}"
        });
    }

    private static string DescribeClientSettings(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return $"client Host={builder.Host} Port={builder.Port} Pooling={builder.Pooling} Multiplexing={builder.Multiplexing} SslMode={builder.SslMode} Timeout={builder.Timeout} CommandTimeout={builder.CommandTimeout} CancellationTimeout={builder.CancellationTimeout} KeepAlive={builder.KeepAlive}";
    }

    private static string DescribeStrategy(Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy strategy)
    {
        int? maxRetry = null;
        object? maxDelay = null;
        for (var type = strategy.GetType(); type != null; type = type.BaseType)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (property.Name == "MaxRetryCount" && property.GetValue(strategy) is int count)
                    maxRetry = count;
                if (property.Name == "MaxRetryDelay")
                    maxDelay = property.GetValue(strategy);
            }
        }

        return $"strategy={strategy.GetType().FullName} retriesOnFailure={strategy.RetriesOnFailure} maxRetryCount={maxRetry?.ToString() ?? "unread"} maxRetryDelay={maxDelay ?? "unread"}";
    }

    private static string DescribeException(Exception exception)
    {
        var text = new StringBuilder();
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (text.Length > 0)
                text.Append(" || ");
            text.Append(current.GetType().FullName).Append(": ").Append(current.Message);
            if (current is NpgsqlException npgsql)
                text.Append(" transient=").Append(npgsql.IsTransient);
            if (current is PostgresException postgres)
                text.Append(" sqlState=").Append(postgres.SqlState);
        }

        return text.ToString();
    }

    private static string Proxied(string direct, int port)
    {
        var builder = new NpgsqlConnectionStringBuilder(direct)
        {
            Host = "127.0.0.1",
            Port = port,
            Pooling = false,
            Multiplexing = false,
            SslMode = SslMode.Disable,
            Timeout = 30,
            CommandTimeout = 30,
            KeepAlive = 0
        };
        return builder.ConnectionString;
    }

    private static void IgnoreContextWarnings(WarningsConfigurationBuilder warnings)
    {
        warnings
            .Ignore(RelationalEventId.PendingModelChangesWarning)
            .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning);
    }

    private sealed class CountingHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.CompletedTask;
        }
    }

    private sealed class ClaimTraceInterceptor : DbCommandInterceptor
    {
        private readonly Func<long> _elapsedMs;
        private readonly object _gate = new();
        private readonly List<ClientEvent> _events = new();
        private readonly List<int> _claimTimeouts = new();

        public ClaimTraceInterceptor(Func<long> elapsedMs) => _elapsedMs = elapsedMs;

        public IReadOnlyList<ClientEvent> Events
        {
            get
            {
                lock (_gate)
                    return _events.ToList();
            }
        }

        public IReadOnlyList<int> ClaimCommandTimeouts
        {
            get
            {
                lock (_gate)
                    return _claimTimeouts.ToList();
            }
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Note("NonQueryExecuting", command, "");
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            Note("NonQueryExecuted", command, "rows=" + result);
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Note("ReaderExecuting", command, "");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            Note("CommandFailed", command, DescribeException(eventData.Exception));
            return base.CommandFailedAsync(command, eventData, cancellationToken);
        }

        private void Note(string phase, DbCommand command, string detail)
        {
            var claim = IsClaim(command.CommandText);
            var sql = command.CommandText.Replace('\r', ' ').Replace('\n', ' ');
            if (sql.Length > 240)
                sql = sql[..240];
            lock (_gate)
            {
                if (claim && phase == "NonQueryExecuting")
                    _claimTimeouts.Add(command.CommandTimeout);
                _events.Add(new ClientEvent(phase, _elapsedMs(), claim, command.CommandTimeout, detail, sql));
            }
        }

        private static bool IsClaim(string sql)
        {
            if (!sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
                || !sql.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var compact = sql.Replace(" ", "", StringComparison.Ordinal)
                .Replace("\n", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal);
            return compact.Contains("AttemptCount\"+1", StringComparison.OrdinalIgnoreCase)
                || compact.Contains("AttemptCount+1", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record ClientEvent(string Phase, long ElapsedMs, bool Claim, int CommandTimeout, string Detail, string Sql);

    private sealed record RunResult(
        int Run,
        string Environment,
        string Strategy,
        string ClientSettings,
        OutboxStatus BeforeStatus,
        int BeforeAttempt,
        OutboxStatus AfterStatus,
        int AfterAttempt,
        int? ProcessedReturn,
        int HandlerCalls,
        Exception? Error,
        long TotalElapsedMs,
        IReadOnlyList<OutboxTimeoutLossProxy.CycleObservation> Cycles,
        IReadOnlyList<OutboxTimeoutLossProxy.Mark> Marks,
        IReadOnlyList<ClientEvent> ClientEvents,
        IReadOnlyList<int> ClaimCommandTimeouts,
        long? CommitObservedMs,
        long? FirstClaimStartedMs,
        long? FirstClaimFailedMs,
        string Trace)
    {
        public OutboxTimeoutLossProxy.CycleObservation? Withheld =>
            Cycles.FirstOrDefault(cycle => cycle.Action == "WithheldUntilClientEnd");
    }
}
