using System.Data.Common;
using Xunit.Abstractions;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.IntegrationTests.Outbox;

/// <summary>
/// Measures the frozen PR2 claim under the production execution strategy when the
/// PostgreSQL response is lost after commit. The proxy is plaintext and the processor
/// is unchanged. This file does not authorize a claim rewrite.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxLostResponseCollection : ICollectionFixture<FullApiPostgresFixture>
{
    public const string Name = "OutboxLostResponse";
}

[Collection(OutboxLostResponseCollection.Name)]
[Trait("Layer", "PostgreSQL")]
[Trait("Feature", "OutboxLostResponse")]
public sealed class OutboxLostResponseCharacterizationTests
{
    private static readonly SemaphoreSlim MigrateGate = new(1, 1);
    private static string? _migratedConnection;

    private readonly string _direct;
    private readonly ITestOutputHelper _output;

    public OutboxLostResponseCharacterizationTests(FullApiPostgresFixture postgres, ITestOutputHelper output)
    {
        _direct = postgres.ConnectionString;
        _output = output;
    }

    [Fact]
    public async Task ScenarioA_FailureBeforeCommit()
    {
        var result = await RunAsync(OutboxResponseLossProxy.LossMode.FailBeforeCommit, maxDrops: int.MaxValue);
        Publish("A", result);

        result.HandlerCalls.Should().Be(0, result.Trace);
        result.AfterStatus.Should().Be(OutboxStatus.Pending, result.Trace);
        result.AfterAttempt.Should().Be(result.BeforeAttempt, result.Trace);
        result.Cycles.Should().Contain(c => c.Action == "DroppedBeforeForward", result.Trace);
        result.Cycles.Should().NotContain(c => c.Action == "DroppedAfterCommit", result.Trace);
        result.Cycles.Where(c => c.Action == "DroppedBeforeForward").Should().AllSatisfy(cycle =>
        {
            cycle.Sql.Should().Contain("UPDATE");
            cycle.CommandTags.Should().BeEmpty();
            cycle.ObservedRowCount.Should().Be(1);
            cycle.ObservedStatus.Should().Be((int)OutboxStatus.Pending);
            cycle.ObservedAttempt.Should().Be(result.BeforeAttempt);
            cycle.ObserverError.Should().BeNull();
        });
        result.ClaimCommandTimeouts.Should().NotBeEmpty(result.Trace);
        result.ClaimCommandTimeouts.Should().OnlyContain(timeout => timeout == 30);
        result.Error.Should().BeOfType<RetryLimitExceededException>(result.Trace);
        result.Error!.InnerException.Should().BeOfType<NpgsqlException>(result.Trace);
        ((NpgsqlException)result.Error.InnerException!).IsTransient.Should().BeTrue();
        ClaimEvents(result, "NonQueryExecuting").Should().Be(4, result.Trace);
        ClaimEvents(result, "CommandFailed").Should().Be(4, result.Trace);
        ClaimEvents(result, "NonQueryExecuted").Should().Be(0, result.Trace);
        result.Cycles.Count(c => c.Action == "DroppedBeforeForward").Should().Be(4, result.Trace);
        result.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(3), result.Trace);
        result.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(20), result.Trace);
        AssertNoExplicitTransaction(result);
    }

    [Fact]
    public async Task ScenarioB_CommitSucceedsResponseLost()
    {
        var result = await RunAsync(OutboxResponseLossProxy.LossMode.LoseCommittedResponse, maxDrops: 1);
        Publish("B", result);

        var dropped = result.Cycles.Where(c => c.Action == "DroppedAfterCommit").ToList();
        dropped.Should().HaveCount(1, result.Trace);
        var cycle = dropped[0];
        cycle.ReadyStatus.Should().Be("I", result.Trace);
        cycle.ObservedRowCount.Should().Be(1, result.Trace);
        cycle.ObservedStatus.Should().Be((int)OutboxStatus.Processing, result.Trace);
        cycle.ObservedAttempt.Should().Be(result.BeforeAttempt + 1, result.Trace);
        cycle.ObserverError.Should().BeNull(result.Trace);
        var updateWasExecuted = cycle.CommandTags.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            || result.Cycles.Any(c =>
                c.ConnectionId == cycle.ConnectionId
                && c.CommandTags.Contains("UPDATE", StringComparison.OrdinalIgnoreCase));
        updateWasExecuted.Should().BeTrue(result.Trace);
        result.ClaimCommandTimeouts.Should().NotBeEmpty(result.Trace);
        result.ClaimCommandTimeouts.Should().OnlyContain(timeout => timeout == 30);
        result.Error.Should().BeNull(result.Trace);
        result.ProcessedReturn.Should().Be(0, result.Trace);
        result.HandlerCalls.Should().Be(0, result.Trace);
        result.AfterStatus.Should().Be(OutboxStatus.Processing, result.Trace);
        result.AfterAttempt.Should().Be(result.BeforeAttempt + 1, result.Trace);
        ClaimEvents(result, "NonQueryExecuting").Should().Be(2, result.Trace);
        ClaimEvents(result, "CommandFailed").Should().Be(1, result.Trace);
        result.ClientEvents.Should().Contain(e => e.Contains("CommandFailed", StringComparison.Ordinal) && e.Contains("claim=True", StringComparison.Ordinal) && e.Contains("transient=True", StringComparison.Ordinal), result.Trace);
        result.ClientEvents.Should().Contain(e => e.Contains("NonQueryExecuted", StringComparison.Ordinal) && e.Contains("claim=True", StringComparison.Ordinal) && e.Contains("rows=0", StringComparison.Ordinal), result.Trace);
        result.ClientEvents.Should().NotContain(e =>
            e.Contains("NonQuery", StringComparison.Ordinal)
            && e.Contains("ProcessedAtUtc", StringComparison.Ordinal));
        result.Cycles.Should().Contain(c => c.Action == "Forwarded" && c.CommandTags.Contains("UPDATE 0", StringComparison.Ordinal) && c.ObservedStatus == (int)OutboxStatus.Processing && c.ObservedAttempt == 1, result.Trace);
        result.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), result.Trace);
        AssertNoExplicitTransaction(result);
    }

    [Fact]
    public async Task ScenarioC_NormalSuccess()
    {
        var result = await RunAsync(OutboxResponseLossProxy.LossMode.Transparent, maxDrops: 0);
        Publish("C", result);

        result.Error.Should().BeNull(result.Trace);
        result.ProcessedReturn.Should().Be(1, result.Trace);
        result.HandlerCalls.Should().Be(1, result.Trace);
        result.AfterStatus.Should().Be(OutboxStatus.Processed, result.Trace);
        result.AfterAttempt.Should().Be(result.BeforeAttempt + 1, result.Trace);
        result.Cycles.Should().NotContain(c => c.Action != "Forwarded", result.Trace);
        result.Cycles.Should().Contain(c =>
            c.Action == "Forwarded"
            && c.ReadyStatus == "I"
            && c.CommandTags.Contains("UPDATE 1", StringComparison.Ordinal),
            result.Trace);
        result.ClaimCommandTimeouts.Should().NotBeEmpty(result.Trace);
        result.ClaimCommandTimeouts.Should().OnlyContain(timeout => timeout == 30);
        ClaimEvents(result, "NonQueryExecuting").Should().Be(1, result.Trace);
        ClaimEvents(result, "CommandFailed").Should().Be(0, result.Trace);
        result.ClientEvents.Should().Contain(e => e.Contains("NonQueryExecuted", StringComparison.Ordinal) && e.Contains("claim=True", StringComparison.Ordinal) && e.Contains("rows=1", StringComparison.Ordinal), result.Trace);
        AssertNoExplicitTransaction(result);
    }

    private async Task<RunResult> RunAsync(OutboxResponseLossProxy.LossMode mode, int maxDrops)
    {
        await EnsureMigratedAsync();
        var id = await SeedAsync();
        var before = await ReloadAsync(id);
        var upstream = new NpgsqlConnectionStringBuilder(_direct);
        if (string.IsNullOrWhiteSpace(upstream.Host))
            throw new InvalidOperationException("Testcontainer connection string has no host.");

        await using var proxy = await OutboxResponseLossProxy.StartAsync(
            upstream.Host,
            upstream.Port,
            _direct,
            mode,
            maxDrops);
        var tracer = new ClaimTraceInterceptor();
        await using var db = CreateProcessorContext(Proxied(_direct, proxy.Port), tracer);
        var strategy = db.Database.CreateExecutionStrategy();
        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        await using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);

        var started = Stopwatch.GetTimestamp();
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

        var elapsed = Stopwatch.GetElapsedTime(started);
        var after = await ReloadAsync(id);
        return new RunResult(
            mode,
            DescribeEnvironment(),
            DescribeStrategy(strategy),
            before.Status,
            before.AttemptCount,
            after.Status,
            after.AttemptCount,
            processed,
            handler.Calls,
            error,
            elapsed,
            proxy.Cycles.ToList(),
            tracer.Events.ToList(),
            tracer.ClaimCommandTimeouts.ToList(),
            proxy.FormatTrace());
    }

    private void Publish(string scenario, RunResult result)
    {
        _output.WriteLine(
            "SCENARIO " + scenario + Environment.NewLine
            + result.Environment + Environment.NewLine
            + result.Strategy + Environment.NewLine
            + $"RESULT beforeStatus={(int)result.BeforeStatus} beforeAttempt={result.BeforeAttempt}" + Environment.NewLine
            + $"RESULT afterStatus={(int)result.AfterStatus} afterAttempt={result.AfterAttempt}" + Environment.NewLine
            + $"RESULT processedReturn={(result.ProcessedReturn?.ToString() ?? "threw")} handlerCalls={result.HandlerCalls} elapsedMs={(long)result.Elapsed.TotalMilliseconds}" + Environment.NewLine
            + "RESULT exception=" + (result.Error == null ? "none" : DescribeException(result.Error)) + Environment.NewLine
            + "INTERCEPTOR" + Environment.NewLine
            + string.Join(Environment.NewLine, result.ClientEvents) + Environment.NewLine
            + "PROXY" + Environment.NewLine
            + result.Trace);
    }

    private static int ClaimEvents(RunResult result, string phase) =>
        result.ClientEvents.Count(e =>
            e.Contains(phase, StringComparison.Ordinal)
            && e.Contains("claim=True", StringComparison.Ordinal));

    private static void AssertNoExplicitTransaction(RunResult result)
    {
        result.Cycles.Should().NotContain(c =>
            c.CommandTags.Contains("BEGIN", StringComparison.OrdinalIgnoreCase)
            || c.CommandTags.Contains("COMMIT", StringComparison.OrdinalIgnoreCase)
            || c.CommandTags.Contains("ROLLBACK", StringComparison.OrdinalIgnoreCase),
            result.Trace);
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
            "lost-response"));
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

    private string DescribeEnvironment()
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
            $"upstreamHost={upstream.Host} upstreamPort={upstream.Port} database={upstream.Database}",
            "processorConnection=127.0.0.1 via plaintext proxy; Pooling=false; Multiplexing=false; SslMode=Disable; CommandTimeout=30; EnableRetryOnFailure(maxRetryCount: 3)"
        });
    }

    private static string DescribeStrategy(IExecutionStrategy strategy)
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

        return $"strategy={strategy.GetType().FullName} retriesOnFailure={strategy.RetriesOnFailure} maxRetryCount={maxRetry?.ToString() ?? "unread"} maxRetryDelay={maxDelay ?? "unread"} strategyAssembly={strategy.GetType().Assembly.GetName().Name} {strategy.GetType().Assembly.GetName().Version}";
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
        private readonly object _gate = new();
        private readonly List<string> _events = new();
        private readonly List<int> _claimTimeouts = new();
        private readonly long _started = Stopwatch.GetTimestamp();

        public IReadOnlyList<string> Events
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
            var elapsed = (long)Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            var sql = command.CommandText.Replace('\r', ' ').Replace('\n', ' ');
            lock (_gate)
            {
                if (claim && phase == "NonQueryExecuting")
                    _claimTimeouts.Add(command.CommandTimeout);
                _events.Add(
                    $"utc={DateTime.UtcNow:HH:mm:ss.fff} t={elapsed}ms {phase} claim={claim} commandTimeout={command.CommandTimeout}s {detail} SQL={sql}");
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

    private sealed record RunResult(
        OutboxResponseLossProxy.LossMode Mode,
        string Environment,
        string Strategy,
        OutboxStatus BeforeStatus,
        int BeforeAttempt,
        OutboxStatus AfterStatus,
        int AfterAttempt,
        int? ProcessedReturn,
        int HandlerCalls,
        Exception? Error,
        TimeSpan Elapsed,
        IReadOnlyList<OutboxResponseLossProxy.CycleObservation> Cycles,
        IReadOnlyList<string> ClientEvents,
        IReadOnlyList<int> ClaimCommandTimeouts,
        string Trace);
}
