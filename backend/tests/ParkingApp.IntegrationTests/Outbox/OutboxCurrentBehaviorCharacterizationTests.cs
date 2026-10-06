using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using ParkingApp.BuildingBlocks.Domain;
using ParkingApp.Identity.Domain.Entities;
using ParkingApp.Infrastructure.Data;
using ParkingApp.Infrastructure.Outbox;
using ParkingApp.Infrastructure.Repositories;
using ParkingApp.IntegrationTests.Support;
using ParkingApp.Marketplace.Domain.Events;

namespace ParkingApp.IntegrationTests.Outbox;

/// <summary>
/// Locks Outbox behavior on PostgreSQL after the PR2 terminal-state correction.
/// Claim, retry, requeue, and transaction boundaries that a later design may change
/// stay covered here. Do not weaken an assertion to hide a regression.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OutboxCharacterizationCollection : ICollectionFixture<FullApiPostgresFixture>
{
    public const string Name = "OutboxCharacterization";
}

[Collection(OutboxCharacterizationCollection.Name)]
[Trait("Layer", "PostgreSQL")]
[Trait("Feature", "OutboxCharacterization")]
public sealed class OutboxCurrentBehaviorCharacterizationTests : IAsyncLifetime
{
    private const string SimulatedFailure = "simulated notification failure";

    private readonly string _connectionString;

    public OutboxCurrentBehaviorCharacterizationTests(FullApiPostgresFixture postgres) =>
        _connectionString = postgres.ConnectionString;

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A1_PendingMessage_IsClaimedOnce_AndBecomesProcessed()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        processed.Should().Be(1);
        scope.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().BeNull();
        row.ProcessedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task A2_ProcessingRow_IsNotDispatched()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Processing;
        message.AttemptCount = 3;
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        processed.Should().Be(0);
        scope.Handler.Calls.Should().Be(0);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processing);
        row.AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task A2_ConcurrentWorkers_OnlyOneClaimsPendingRow()
    {
        await ClearMessagesAsync();
        await using var seed = CreateDb();
        var message = await EnqueueCancelledAsync(seed);
        var shared = new CountingHandler();
        await using var leftDb = CreateDb();
        await using var rightDb = CreateDb();
        await using var left = CreateProcessor(leftDb, shared);
        await using var right = CreateProcessor(rightDb, shared);

        await Task.WhenAll(
                left.Processor.ProcessByIdsAsync(new[] { message.Id }),
                right.Processor.ProcessByIdsAsync(new[] { message.Id }))
            .WaitAsync(TimeSpan.FromSeconds(30));

        shared.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task B1_SuccessfulClaim_IncrementsAttemptCountByOne()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.AttemptCount = 6;
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(7);
        scope.Handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task B2_TenthFailedAttempt_BecomesFailed_AndIsNotPolled()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.AttemptCount = 9;
        message.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await using var failing = CreateFailingProcessor(db);

        var started = DateTime.UtcNow;
        var processed = await failing.Processor.ProcessByIdsAsync(new[] { message.Id });
        var finished = DateTime.UtcNow;

        processed.Should().Be(0);
        failing.Handler.Calls.Should().Be(1);
        var failed = await ReloadAsync(message.Id);
        failed.Status.Should().Be(OutboxStatus.Failed);
        failed.AttemptCount.Should().Be(10);
        failed.LastError.Should().Contain(SimulatedFailure);
        AssertBackoff(failed.AvailableAfterUtc, started, finished, delaySeconds: 256);

        await using var edit = CreateDb();
        var editable = await edit.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        editable.AvailableAfterUtc = DateTime.UtcNow;
        await edit.SaveChangesAsync();

        await using var pollDb = CreateDb();
        var sibling = await EnqueueCancelledAsync(pollDb);
        await using var poller = CreateProcessor(pollDb);
        await poller.Processor.ProcessPendingAsync();

        poller.Handler.Calls.Should().Be(1);
        (await ReloadAsync(sibling.Id)).Status.Should().Be(OutboxStatus.Processed);
        var stillFailed = await ReloadAsync(message.Id);
        stillFailed.Status.Should().Be(OutboxStatus.Failed);
        stillFailed.AttemptCount.Should().Be(10);
    }

    [Fact]
    public async Task B3_FailedAttemptTen_IsNotPolled()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var exhausted = await EnqueueCancelledAsync(db);
        exhausted.Status = OutboxStatus.Failed;
        exhausted.AttemptCount = 10;
        exhausted.AvailableAfterUtc = DateTime.UtcNow;
        var eligible = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        (await ReloadAsync(eligible.Id)).Status.Should().Be(OutboxStatus.Processed);
        var row = await ReloadAsync(exhausted.Id);
        row.Status.Should().Be(OutboxStatus.Failed);
        row.AttemptCount.Should().Be(10);
    }

    [Fact]
    public async Task C1_FailureBelowCeiling_ReturnsToPending_AndRetriesWhenDue()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.AttemptCount = 4;
        message.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await using var failing = CreateFailingProcessor(db);

        var started = DateTime.UtcNow;
        var processed = await failing.Processor.ProcessByIdsAsync(new[] { message.Id });
        var finished = DateTime.UtcNow;

        processed.Should().Be(0);
        failing.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Pending);
        row.AttemptCount.Should().Be(5);
        row.LastError.Should().Contain(SimulatedFailure);
        row.ProcessedAtUtc.Should().BeNull();
        AssertBackoff(row.AvailableAfterUtc, started, finished, delaySeconds: 32);

        await using var pollDb = CreateDb();
        var sibling = await EnqueueCancelledAsync(pollDb);
        await using var poller = CreateProcessor(pollDb);
        await poller.Processor.ProcessPendingAsync();

        poller.Handler.Calls.Should().Be(1);
        (await ReloadAsync(sibling.Id)).Status.Should().Be(OutboxStatus.Processed);
        var waiting = await ReloadAsync(message.Id);
        waiting.Status.Should().Be(OutboxStatus.Pending);
        waiting.AttemptCount.Should().Be(5);

        await using var edit = CreateDb();
        var editable = await edit.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        editable.AvailableAfterUtc = DateTime.UtcNow.AddSeconds(-1);
        await edit.SaveChangesAsync();

        await using var retryDb = CreateDb();
        await using var retry = CreateProcessor(retryDb);
        await retry.Processor.ProcessPendingAsync();

        retry.Handler.Calls.Should().Be(1);
        var retried = await ReloadAsync(message.Id);
        retried.Status.Should().Be(OutboxStatus.Processed);
        retried.AttemptCount.Should().Be(6);
    }

    [Fact]
    public async Task C2_BackoffMatchesCurrentPowerFormula()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var first = await EnqueueCancelledAsync(db);
        var ninth = await EnqueueCancelledAsync(db);
        ninth.AttemptCount = 8;
        await db.SaveChangesAsync();
        await using var failing = CreateFailingProcessor(db);

        var started = DateTime.UtcNow;
        await failing.Processor.ProcessByIdsAsync(new[] { first.Id });
        var finished = DateTime.UtcNow;
        var firstRow = await ReloadAsync(first.Id);
        firstRow.Status.Should().Be(OutboxStatus.Pending);
        firstRow.AttemptCount.Should().Be(1);
        firstRow.LastError.Should().Contain(SimulatedFailure);
        // min(300, 2 ^ min(1, 8)) = 2. The 300 second cap is not reachable while the exponent is capped at 8.
        AssertBackoff(firstRow.AvailableAfterUtc, started, finished, delaySeconds: 2);

        started = DateTime.UtcNow;
        await failing.Processor.ProcessByIdsAsync(new[] { ninth.Id });
        finished = DateTime.UtcNow;
        var ninthRow = await ReloadAsync(ninth.Id);
        ninthRow.Status.Should().Be(OutboxStatus.Pending);
        ninthRow.AttemptCount.Should().Be(9);
        ninthRow.LastError.Should().Contain(SimulatedFailure);
        // min(300, 2 ^ min(9, 8)) = 256, not 2^9.
        AssertBackoff(ninthRow.AvailableAfterUtc, started, finished, delaySeconds: 256);
    }

    [Fact]
    public async Task C3_FutureAvailableAfter_IsNotPolled()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var future = await EnqueueCancelledAsync(db);
        future.Status = OutboxStatus.Failed;
        future.AttemptCount = 2;
        future.AvailableAfterUtc = DateTime.UtcNow.AddHours(2);
        var ready = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        var skipped = await ReloadAsync(future.Id);
        skipped.Status.Should().Be(OutboxStatus.Failed);
        skipped.AttemptCount.Should().Be(2);
        (await ReloadAsync(ready.Id)).Status.Should().Be(OutboxStatus.Processed);
    }

    [Fact]
    public async Task C4_AvailableAfterEqualToNow_IsPolled()
    {
        await ClearMessagesAsync();
        var capture = new SqlCapture();
        await using var db = CreateDb(capture);
        var message = await EnqueueCancelledAsync(db);
        message.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        capture.Commands.Clear();
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        (await ReloadAsync(message.Id)).Status.Should().Be(OutboxStatus.Processed);
        var poll = PollSelect(capture);
        poll.Should().Contain("<=", "the poll predicate is AvailableAfterUtc <= now. SQL: " + poll);
        poll.Should().Contain("AvailableAfterUtc");
    }

    [Fact]
    public async Task C5_NullAvailableAfter_IsPolled()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.AvailableAfterUtc = null;
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Poll_FailedStatusBelowCeiling_IsEligible()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Failed;
        message.AttemptCount = 3;
        message.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(4);
    }

    [Fact]
    public async Task D1_ProcessedSibling_SuppressesDispatch_WithoutIncrementingAttempt()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var sibling = await EnqueueCancelledAsync(db);
        var pending = await EnqueueCancelledAsync(db);
        var key = "sibling-" + Guid.NewGuid().ToString("N");
        sibling.IdempotencyKey = key;
        sibling.Status = OutboxStatus.Processed;
        sibling.ProcessedAtUtc = DateTime.UtcNow;
        pending.IdempotencyKey = key;
        pending.Status = OutboxStatus.Pending;
        pending.AttemptCount = 4;
        pending.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { pending.Id });

        processed.Should().Be(0);
        scope.Handler.Calls.Should().Be(0);
        var row = await ReloadAsync(pending.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(4);
        row.ProcessedAtUtc.Should().NotBeNull();
        (await ReloadAsync(sibling.Id)).Status.Should().Be(OutboxStatus.Processed);
    }

    [Fact]
    public async Task D2_SameRowRedelivery_IsNotSuppressed()
    {
        await ClearMessagesAsync();
        var capture = new SqlCapture();
        await using var db = CreateDb(capture);
        var message = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);
        await scope.Processor.ProcessByIdsAsync(new[] { message.Id });
        scope.Handler.Calls.Should().Be(1);

        message.Status = OutboxStatus.Pending;
        message.AvailableAfterUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        capture.Commands.Clear();

        await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        scope.Handler.Calls.Should().Be(2);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(2);
        var dump = string.Join("\n---\n", capture.Commands);
        var idempotencyLookup = capture.Commands.FirstOrDefault(c =>
            c.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase) &&
            !c.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        idempotencyLookup.Should().NotBeNull("sibling lookup SQL was not captured. Commands:\n" + dump);
        idempotencyLookup!.Should().MatchRegex(
            @"""Id""\s*<>|!=",
            "the sibling lookup excludes the current id. SQL: " + idempotencyLookup);
    }

    [Fact]
    public async Task E1_PaymentKey_OmitsOccurrenceTicks()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var evt = new PaymentCompletedEvent(
            paymentId, bookingId, Guid.NewGuid(), Guid.NewGuid(), "BK", 10m, "INR", false);

        var message = await EnqueueAsync(db, evt);

        message.IdempotencyKey.Split(':').Should().Equal(
            nameof(PaymentCompletedEvent),
            "payment",
            paymentId.ToString("N"));
        message.IdempotencyKey.Should().NotContain(bookingId.ToString("N"));
    }

    [Fact]
    public async Task E2_BookingKey_IncludesOccurredOnTicks()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var bookingId = Guid.NewGuid();
        var evt = new BookingCancelledEvent(bookingId, Guid.NewGuid(), Guid.NewGuid(), "BK", "reason");

        var message = await EnqueueAsync(db, evt);

        message.IdempotencyKey.Split(':').Should().Equal(
            nameof(BookingCancelledEvent),
            bookingId.ToString("N"),
            evt.OccurredOn.Ticks.ToString());
    }

    [Fact]
    public async Task E3_ParkingSpaceKey_IncludesOccurredOnTicks()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var parkingSpaceId = Guid.NewGuid();
        var evt = new ParkingSpaceCreatedEvent(parkingSpaceId, Guid.NewGuid(), "Lot");

        var message = await EnqueueAsync(db, evt);

        message.IdempotencyKey.Split(':').Should().Equal(
            nameof(ParkingSpaceCreatedEvent),
            parkingSpaceId.ToString("N"),
            evt.OccurredOn.Ticks.ToString());
    }

    [Fact]
    public async Task E4_IdempotencyIndex_IsNotUnique()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(OutboxMessage));
        entity.Should().NotBeNull();
        entity!.GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id",
            "TypeName",
            "Payload",
            "IdempotencyKey",
            "Status",
            "AttemptCount",
            "LastError",
            "CreatedAtUtc",
            "AvailableAfterUtc",
            "ProcessedAtUtc");
        var indexes = entity.GetIndexes().ToList();
        indexes.Select(i => i.GetDatabaseName()).Should().BeEquivalentTo(
            "IX_OutboxMessages_IdempotencyKey",
            "IX_OutboxMessages_Status_AvailableAfterUtc",
            "IX_OutboxMessages_CreatedAtUtc");
        indexes.Should().OnlyContain(i => !i.IsUnique);

        var key = "dup-" + Guid.NewGuid().ToString("N");
        db.OutboxMessages.AddRange(
            NewRow(key),
            NewRow(key));
        var act = () => db.SaveChangesAsync();
        await act.Should().NotThrowAsync();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            SELECT i.indisunique
            FROM pg_index i
            JOIN pg_class idx ON idx.oid = i.indexrelid
            JOIN pg_class tbl ON tbl.oid = i.indrelid
            JOIN pg_namespace n ON n.oid = tbl.relnamespace
            WHERE n.nspname = 'public'
              AND tbl.relname = 'OutboxMessages'
              AND idx.relname = 'IX_OutboxMessages_IdempotencyKey'
            """,
            connection);
        var unique = await command.ExecuteScalarAsync();
        unique.Should().Be(false);
    }

    [Fact]
    public async Task F1_Requeue_DoesNotResetAttemptCount_AndBecomesEligible()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Failed;
        message.AttemptCount = 5;
        message.LastError = "err";
        message.ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        message.AvailableAfterUtc = DateTime.UtcNow.AddHours(3);
        await db.SaveChangesAsync();
        var store = new OutboxAdminStore(db);
        var started = DateTime.UtcNow;

        var ok = await store.RequeueAsync(message.Id);

        ok.Should().BeTrue();
        var requeued = await ReloadAsync(message.Id);
        requeued.Status.Should().Be(OutboxStatus.Pending);
        requeued.AttemptCount.Should().Be(5);
        requeued.LastError.Should().BeNull();
        requeued.ProcessedAtUtc.Should().BeNull();
        requeued.AvailableAfterUtc.Should().NotBeNull();
        requeued.AvailableAfterUtc!.Value.Should().BeOnOrAfter(started.AddSeconds(-1));
        requeued.AvailableAfterUtc.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(2));

        await using var scope = CreateProcessor(db);
        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(6);
    }

    [Fact]
    public async Task F2_RequeueAttemptTen_StaysInvisibleToPoll()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Failed;
        message.AttemptCount = 10;
        message.LastError = "exhausted";
        message.AvailableAfterUtc = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        var ok = await new OutboxAdminStore(db).RequeueAsync(message.Id);

        ok.Should().BeTrue();
        var requeued = await ReloadAsync(message.Id);
        requeued.Status.Should().Be(OutboxStatus.Pending);
        requeued.AttemptCount.Should().Be(10);
        requeued.LastError.Should().BeNull();

        var sibling = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);
        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        (await ReloadAsync(sibling.Id)).Status.Should().Be(OutboxStatus.Processed);
        var hidden = await ReloadAsync(message.Id);
        hidden.Status.Should().Be(OutboxStatus.Pending);
        hidden.AttemptCount.Should().Be(10);
    }

    [Fact]
    public async Task F3_ProcessedRow_CannotBeRequeued()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Processed;
        message.AttemptCount = 2;
        message.LastError = "keep";
        message.ProcessedAtUtc = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        var ok = await new OutboxAdminStore(db).RequeueAsync(message.Id);

        ok.Should().BeFalse();
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(2);
        row.LastError.Should().Be("keep");
        row.ProcessedAtUtc.Should().Be(new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task F4_ProcessingRow_CanBeRequeued()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Processing;
        message.AttemptCount = 4;
        message.LastError = "mid";
        message.ProcessedAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var ok = await new OutboxAdminStore(db).RequeueAsync(message.Id);

        ok.Should().BeTrue();
        var requeued = await ReloadAsync(message.Id);
        requeued.Status.Should().Be(OutboxStatus.Pending);
        requeued.AttemptCount.Should().Be(4);
        requeued.LastError.Should().BeNull();
        requeued.ProcessedAtUtc.Should().BeNull();

        await using var scope = CreateProcessor(db);
        await scope.Processor.ProcessPendingAsync();

        scope.Handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(5);
    }

    [Fact]
    public async Task G1_ProcessByIds_ProcessesOnlySuppliedIds()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var first = await EnqueueCancelledAsync(db);
        var second = await EnqueueCancelledAsync(db);
        var excluded = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { first.Id, second.Id });

        processed.Should().Be(2);
        scope.Handler.Calls.Should().Be(2);
        (await ReloadAsync(first.Id)).AttemptCount.Should().Be(1);
        (await ReloadAsync(second.Id)).Status.Should().Be(OutboxStatus.Processed);
        var leftOut = await ReloadAsync(excluded.Id);
        leftOut.Status.Should().Be(OutboxStatus.Pending);
        leftOut.AttemptCount.Should().Be(0);
    }

    [Fact(Timeout = 180000)]
    public async Task G2_ProcessByIds_DoesNotCapAtFifty()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var excluded = await EnqueueCancelledAsync(db);
        excluded.CreatedAtUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var ids = new List<Guid>(51);
        for (var i = 0; i < 51; i++)
        {
            var message = await EnqueueCancelledAsync(db);
            message.CreatedAtUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i);
            ids.Add(message.Id);
        }

        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(ids);

        processed.Should().Be(51);
        scope.Handler.Calls.Should().Be(51);
        var excludedRow = await ReloadAsync(excluded.Id);
        excludedRow.Status.Should().Be(OutboxStatus.Pending);
        excludedRow.AttemptCount.Should().Be(0);
        await using var verify = CreateDb();
        var claimed = await verify.OutboxMessages.AsNoTracking()
            .CountAsync(m => ids.Contains(m.Id) && m.Status == OutboxStatus.Processed && m.AttemptCount == 1);
        claimed.Should().Be(51);
    }

    [Fact]
    public async Task G3_ProcessByIds_DoesNotClaimProcessingRow()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var processing = await EnqueueCancelledAsync(db);
        processing.Status = OutboxStatus.Processing;
        processing.AttemptCount = 2;
        var pending = await EnqueueCancelledAsync(db);
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { processing.Id, pending.Id });

        processed.Should().Be(1);
        scope.Handler.Calls.Should().Be(1);
        var skipped = await ReloadAsync(processing.Id);
        skipped.Status.Should().Be(OutboxStatus.Processing);
        skipped.AttemptCount.Should().Be(2);
        var claimed = await ReloadAsync(pending.Id);
        claimed.Status.Should().Be(OutboxStatus.Processed);
        claimed.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task G4_ProcessByIds_DoesNotDispatchProcessedRow()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        message.Status = OutboxStatus.Processed;
        message.AttemptCount = 1;
        message.ProcessedAtUtc = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        processed.Should().Be(0);
        scope.Handler.Calls.Should().Be(0);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
        row.ProcessedAtUtc.Should().Be(new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task H1_ProcessingWaitsUntilTransactionCommit()
    {
        await ClearMessagesAsync();
        Guid userId = Guid.Empty;
        try
        {
            await using var db = CreateDb();
            await using var scope = CreateProcessor(db);
            var uow = new UnitOfWork(db, new OutboxWriter(db), scope.Processor, NullLogger<UnitOfWork>.Instance);
            var user = User.Register($"h1-{Guid.NewGuid():N}@example.com", "hash", "H", "One", "9000000001");
            userId = user.Id;
            var bookingId = Guid.NewGuid();
            user.AddDomainEvent(new BookingCancelledEvent(bookingId, user.Id, Guid.NewGuid(), "BK", "reason"));
            db.Users.Add(user);

            await uow.BeginTransactionAsync();
            await uow.SaveChangesAsync();

            scope.Handler.Calls.Should().Be(0);
            var stagedKey = db.OutboxMessages.Local.Single().IdempotencyKey;
            await using var outsider = CreateDb();
            (await outsider.OutboxMessages.CountAsync(m => m.IdempotencyKey == stagedKey)).Should().Be(0);
            (await outsider.Users.IgnoreQueryFilters().CountAsync(u => u.Id == userId)).Should().Be(0);

            await uow.CommitTransactionAsync();

            scope.Handler.Calls.Should().Be(1);
            var row = await outsider.OutboxMessages.AsNoTracking().SingleAsync(m => m.IdempotencyKey == stagedKey);
            row.Status.Should().Be(OutboxStatus.Processed);
            row.AttemptCount.Should().Be(1);
            (await outsider.Users.IgnoreQueryFilters().CountAsync(u => u.Id == userId)).Should().Be(1);
        }
        finally
        {
            await DeleteUserAsync(userId);
        }
    }

    [Fact]
    public async Task H2_Rollback_DoesNotProcessOutbox()
    {
        await ClearMessagesAsync();
        Guid userId = Guid.Empty;
        try
        {
            await using var db = CreateDb();
            await using var scope = CreateProcessor(db);
            var uow = new UnitOfWork(db, new OutboxWriter(db), scope.Processor, NullLogger<UnitOfWork>.Instance);
            var user = User.Register($"h2-{Guid.NewGuid():N}@example.com", "hash", "H", "Two", "9000000002");
            userId = user.Id;
            user.AddDomainEvent(new BookingCancelledEvent(Guid.NewGuid(), user.Id, Guid.NewGuid(), "BK", "reason"));
            db.Users.Add(user);

            await uow.BeginTransactionAsync();
            await uow.SaveChangesAsync();
            scope.Handler.Calls.Should().Be(0);
            var stagedKey = db.OutboxMessages.Local.Single().IdempotencyKey;

            await uow.RollbackTransactionAsync();
            await uow.CommitTransactionAsync();

            scope.Handler.Calls.Should().Be(0);
            await using var outsider = CreateDb();
            (await outsider.OutboxMessages.CountAsync(m => m.IdempotencyKey == stagedKey)).Should().Be(0);
            (await outsider.Users.IgnoreQueryFilters().CountAsync(u => u.Id == userId)).Should().Be(0);
        }
        finally
        {
            await DeleteUserAsync(userId);
        }
    }

    [Fact]
    public async Task Cancellation_AfterClaim_IgnoresCallerToken()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        using var cts = new CancellationTokenSource();
        var handler = new CancelAfterStartHandler(cts);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        await using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);

        var processed = await processor.ProcessByIdsAsync(new[] { message.Id }, cts.Token);

        processed.Should().Be(1);
        handler.Seen.CanBeCanceled.Should().BeFalse();
        handler.Seen.Should().Be(CancellationToken.None);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task TerminalSave_IsGuardedByStatusAndAttempt()
    {
        await ClearMessagesAsync();
        var capture = new SqlCapture();
        await using var db = CreateDb(capture);
        var message = await EnqueueCancelledAsync(db);
        capture.Commands.Clear();
        await using var scope = CreateProcessor(db);

        await scope.Processor.ProcessByIdsAsync(new[] { message.Id });

        var dump = string.Join("\n---\n", capture.Commands);
        var updates = capture.Commands
            .Where(c => c.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                        c.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase))
            .ToList();
        updates.Should().NotBeEmpty("expected the claim and terminal updates. SQL:\n" + dump);
        var claim = updates.Single(c => c.Contains("+ 1", StringComparison.Ordinal) || c.Contains("+1", StringComparison.Ordinal));
        TrailingWhere(claim).Should().Contain("Status", "claim SQL:\n" + claim);
        claim.Should().NotContain("RETURNING", "claim SQL:\n" + claim);
        claim.Should().NotContain("FOR UPDATE", "claim SQL:\n" + claim);

        var terminal = updates.Single(c => !c.Contains("+ 1", StringComparison.Ordinal) && !c.Contains("+1", StringComparison.Ordinal));
        var terminalWhere = TrailingWhere(terminal);
        terminalWhere.Should().Contain("\"Id\"");
        terminalWhere.Should().Contain("Status", "terminal SQL:\n" + terminal);
        terminalWhere.Should().Contain("AttemptCount", "terminal SQL:\n" + terminal);
        terminal.Should().NotContain("RETURNING");
        terminal.Should().NotContain("FOR UPDATE");
        capture.Commands.Should().NotContain(c => c.Contains("SKIP LOCKED", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TerminalSave_StaleTrackedContext_CanOverwriteProcessedRow()
    {
        await ClearMessagesAsync();
        await using var worker = CreateDb();
        var message = await EnqueueCancelledAsync(worker);
        await using var stale = CreateDb();
        var tracked = await stale.OutboxMessages.SingleAsync(m => m.Id == message.Id);
        await using var scope = CreateProcessor(worker);
        await scope.Processor.ProcessByIdsAsync(new[] { message.Id });
        (await ReloadAsync(message.Id)).Status.Should().Be(OutboxStatus.Processed);

        tracked.Status = OutboxStatus.Failed;
        tracked.LastError = "stale failure";
        await stale.SaveChangesAsync();

        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Failed);
        row.LastError.Should().Be("stale failure");
        row.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task SameContextSaveChanges_DoesNotFlushClaimedOutboxRow()
    {
        await ClearMessagesAsync();
        Guid userId = Guid.Empty;
        try
        {
            var capture = new SqlCapture();
            await using var db = CreateDb(capture);
            var message = await EnqueueCancelledAsync(db);
            var user = User.Register(
                $"pr2-{Guid.NewGuid():N}@example.com",
                "hash",
                "Save",
                "ThenFail",
                Random.Shared.Next(100000000, 999999999).ToString());
            userId = user.Id;
            var handler = new SaveThenFailHandler(db, message.Id, user);
            var services = new ServiceCollection();
            services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
            await using var provider = services.BuildServiceProvider();
            var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);
            capture.Commands.Clear();

            var started = DateTime.UtcNow;
            var processed = await processor.ProcessByIdsAsync(new[] { message.Id });
            var finished = DateTime.UtcNow;

            processed.Should().Be(0);
            handler.ClaimedRowTracked.Should().BeFalse();
            var row = await ReloadAsync(message.Id);
            row.Status.Should().Be(OutboxStatus.Pending);
            row.AttemptCount.Should().Be(1);
            row.LastError.Should().Contain(SimulatedFailure);
            row.ProcessedAtUtc.Should().BeNull();
            AssertBackoff(row.AvailableAfterUtc, started, finished, delaySeconds: 2);

            await using var verify = CreateDb();
            (await verify.Users.IgnoreQueryFilters().CountAsync(u => u.Id == userId)).Should().Be(1);

            var outboxUpdates = capture.Commands
                .Where(c => c.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                            c.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase))
                .ToList();
            outboxUpdates.Should().HaveCount(2, "SQL:\n" + string.Join("\n---\n", capture.Commands));
            var terminal = outboxUpdates.Single(c =>
                !c.Contains("+ 1", StringComparison.Ordinal) && !c.Contains("+1", StringComparison.Ordinal));
            var terminalWhere = TrailingWhere(terminal);
            terminalWhere.Should().Contain("\"Id\"");
            terminalWhere.Should().Contain("Status");
            terminalWhere.Should().Contain("AttemptCount");
        }
        finally
        {
            await DeleteUserAsync(userId);
        }
    }

    [Fact]
    public async Task TerminalUpdate_ZeroRows_DoesNotOverwriteNewerState()
    {
        await ClearMessagesAsync();
        var capture = new SqlCapture();
        await using var db = CreateDb(capture);
        var message = await EnqueueCancelledAsync(db);
        var handler = new ReplaceThenFailHandler(db, message.Id);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        await using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);
        capture.Commands.Clear();

        var processed = await processor.ProcessByIdsAsync(new[] { message.Id });

        processed.Should().Be(0);
        handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Failed);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().Be("newer-state");
        row.ProcessedAtUtc.Should().BeNull();

        var fenced = capture.Commands
            .Where(c =>
                c.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) &&
                c.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase) &&
                !c.Contains("+ 1", StringComparison.Ordinal) &&
                !c.Contains("+1", StringComparison.Ordinal) &&
                TrailingWhere(c).Contains("AttemptCount", StringComparison.Ordinal))
            .ToList();
        fenced.Should().ContainSingle("SQL:\n" + string.Join("\n---\n", capture.Commands));
        var where = TrailingWhere(fenced[0]);
        where.Should().Contain("\"Id\"");
        where.Should().Contain("Status");
        where.Should().Contain("AttemptCount");
        fenced[0].Should().NotContain("RETURNING");
    }

    [Fact]
    public async Task TerminalUpdate_ZeroRows_MatchingCompletionIsApplied()
    {
        await ClearMessagesAsync();
        await using var db = CreateDb();
        var message = await EnqueueCancelledAsync(db);
        var stamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var handler = new AlreadyProcessedHandler(db, message.Id, stamp);
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        await using var provider = services.BuildServiceProvider();
        var processor = new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance);

        var processed = await processor.ProcessByIdsAsync(new[] { message.Id });

        processed.Should().Be(1);
        handler.Calls.Should().Be(1);
        var row = await ReloadAsync(message.Id);
        row.Status.Should().Be(OutboxStatus.Processed);
        row.AttemptCount.Should().Be(1);
        row.LastError.Should().BeNull();
        row.ProcessedAtUtc.Should().Be(stamp);
    }

    [Fact]
    public async Task Poll_SelectsOrderedIds_ThenLoadsThoseRows()
    {
        await ClearMessagesAsync();
        var capture = new SqlCapture();
        await using var db = CreateDb(capture);
        var oldest = await EnqueueCancelledAsync(db);
        var middle = await EnqueueCancelledAsync(db);
        var newest = await EnqueueCancelledAsync(db);
        oldest.CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc);
        middle.CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 2, DateTimeKind.Utc);
        newest.CreatedAtUtc = new DateTime(2024, 1, 1, 0, 0, 3, DateTimeKind.Utc);
        await db.SaveChangesAsync();
        capture.Commands.Clear();
        await using var scope = CreateProcessor(db);

        var processed = await scope.Processor.ProcessPendingAsync(batchSize: 2);

        processed.Should().Be(2);
        scope.Handler.Calls.Should().Be(2);
        (await ReloadAsync(oldest.Id)).Status.Should().Be(OutboxStatus.Processed);
        (await ReloadAsync(middle.Id)).Status.Should().Be(OutboxStatus.Processed);
        var left = await ReloadAsync(newest.Id);
        left.Status.Should().Be(OutboxStatus.Pending);
        left.AttemptCount.Should().Be(0);

        var poll = PollSelect(capture);
        var selectList = poll[..poll.IndexOf("FROM", StringComparison.OrdinalIgnoreCase)];
        selectList.Should().Contain("\"Id\"");
        selectList.Should().NotContain("Payload");
        poll.Should().Contain("CreatedAtUtc");
        poll.Should().Contain("ORDER BY");
        poll.Should().Contain("LIMIT");
        poll.Should().Contain("AttemptCount");
        poll.Should().MatchRegex(@"""AttemptCount""\s*<");
        poll.Should().NotContain("FOR UPDATE");

        var fullLoad = capture.Commands.First(c =>
            c.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        fullLoad.Should().Contain("CreatedAtUtc");
        fullLoad.Should().NotContain("LIMIT");
        capture.Commands.Should().NotContain(c => c.Contains("SKIP LOCKED", StringComparison.OrdinalIgnoreCase));
    }

    private ApplicationDbContext CreateDb(SqlCapture? capture = null)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.CommandTimeout(120);
                npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.GetName().Name);
            })
            .ConfigureWarnings(warnings => warnings
                .Ignore(RelationalEventId.PendingModelChangesWarning)
                .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        if (capture != null)
            builder.AddInterceptors(capture);
        return new ApplicationDbContext(builder.Options);
    }

    private async Task ClearMessagesAsync()
    {
        await using var db = CreateDb();
        await db.OutboxMessages.ExecuteDeleteAsync();
    }

    private async Task DeleteUserAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return;
        await using var db = CreateDb();
        await db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).ExecuteDeleteAsync();
    }

    private async Task<OutboxMessage> ReloadAsync(Guid id)
    {
        await using var db = CreateDb();
        return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == id);
    }

    private static async Task<OutboxMessage> EnqueueCancelledAsync(ApplicationDbContext db) =>
        await EnqueueAsync(db, new BookingCancelledEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "BK", "reason"));

    private static async Task<OutboxMessage> EnqueueAsync(ApplicationDbContext db, IDomainEvent domainEvent)
    {
        var before = db.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity.Id).ToHashSet();
        new OutboxWriter(db).Enqueue(domainEvent);
        await db.SaveChangesAsync();
        return db.ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity).Single(m => !before.Contains(m.Id));
    }

    private static ProcessorScope CreateProcessor(ApplicationDbContext db, CountingHandler? handler = null)
    {
        handler ??= new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        var provider = services.BuildServiceProvider();
        return new ProcessorScope(
            new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance),
            handler,
            provider);
    }

    private static FailingScope CreateFailingProcessor(ApplicationDbContext db)
    {
        var handler = new FailingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventHandler<BookingCancelledEvent>>(handler);
        var provider = services.BuildServiceProvider();
        return new FailingScope(
            new OutboxProcessor(db, provider, NullLogger<OutboxProcessor>.Instance),
            handler,
            provider);
    }

    private static OutboxMessage NewRow(string idempotencyKey) => new()
    {
        TypeName = "Characterization",
        Payload = "{}",
        IdempotencyKey = idempotencyKey,
        Status = OutboxStatus.Pending,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static void AssertBackoff(DateTime? actual, DateTime startedUtc, DateTime finishedUtc, double delaySeconds)
    {
        actual.Should().NotBeNull();
        var pad = delaySeconds <= 4 ? 0.75 : 5;
        actual!.Value.Should().BeOnOrAfter(startedUtc.AddSeconds(delaySeconds - pad));
        actual.Value.Should().BeOnOrBefore(finishedUtc.AddSeconds(delaySeconds + pad));
    }

    private static string PollSelect(SqlCapture capture)
    {
        var poll = capture.Commands.FirstOrDefault(c =>
            c.Contains("SELECT", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase) &&
            !c.Contains("Payload", StringComparison.OrdinalIgnoreCase));
        poll.Should().NotBeNull("poll SQL was:\n" + string.Join("\n---\n", capture.Commands));
        return poll!;
    }

    private static string TrailingWhere(string sql)
    {
        var index = sql.LastIndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : sql[index..];
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

    private sealed class FailingHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private int _calls;
        public int Calls => Volatile.Read(ref _calls);

        public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            throw new InvalidOperationException(SimulatedFailure);
        }
    }

    private sealed class CancelAfterStartHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private readonly CancellationTokenSource _cancellation;

        public CancelAfterStartHandler(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public CancellationToken Seen { get; private set; } = new(canceled: true);

        public Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Seen = cancellationToken;
            _cancellation.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class SaveThenFailHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private readonly ApplicationDbContext _db;
        private readonly Guid _claimedId;
        private readonly User _user;

        public SaveThenFailHandler(ApplicationDbContext db, Guid claimedId, User user)
        {
            _db = db;
            _claimedId = claimedId;
            _user = user;
        }

        public bool ClaimedRowTracked { get; private set; }

        public async Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            ClaimedRowTracked = _db.ChangeTracker.Entries<OutboxMessage>()
                .Any(e => e.Entity.Id == _claimedId && e.State != EntityState.Detached);
            _db.Users.Add(_user);
            await _db.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(SimulatedFailure);
        }
    }

    private sealed class ReplaceThenFailHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private readonly ApplicationDbContext _db;
        private readonly Guid _id;
        private int _calls;

        public ReplaceThenFailHandler(ApplicationDbContext db, Guid id)
        {
            _db = db;
            _id = id;
        }

        public int Calls => Volatile.Read(ref _calls);

        public async Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await _db.OutboxMessages
                .Where(m => m.Id == _id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, OutboxStatus.Failed)
                    .SetProperty(x => x.LastError, "newer-state"),
                    cancellationToken);
            throw new InvalidOperationException(SimulatedFailure);
        }
    }

    private sealed class AlreadyProcessedHandler : IDomainEventHandler<BookingCancelledEvent>
    {
        private readonly ApplicationDbContext _db;
        private readonly Guid _id;
        private readonly DateTime _stamp;
        private int _calls;

        public AlreadyProcessedHandler(ApplicationDbContext db, Guid id, DateTime stamp)
        {
            _db = db;
            _id = id;
            _stamp = stamp;
        }

        public int Calls => Volatile.Read(ref _calls);

        public async Task HandleAsync(BookingCancelledEvent domainEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await _db.OutboxMessages
                .Where(m => m.Id == _id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, OutboxStatus.Processed)
                    .SetProperty(x => x.ProcessedAtUtc, _stamp)
                    .SetProperty(x => x.LastError, (string?)null),
                    cancellationToken);
        }
    }

    private sealed class ProcessorScope : IAsyncDisposable
    {
        public ProcessorScope(OutboxProcessor processor, CountingHandler handler, ServiceProvider services)
        {
            Processor = processor;
            Handler = handler;
            Services = services;
        }

        public OutboxProcessor Processor { get; }
        public CountingHandler Handler { get; }
        public ServiceProvider Services { get; }
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    private sealed class FailingScope : IAsyncDisposable
    {
        public FailingScope(OutboxProcessor processor, FailingHandler handler, ServiceProvider services)
        {
            Processor = processor;
            Handler = handler;
            Services = services;
        }

        public OutboxProcessor Processor { get; }
        public FailingHandler Handler { get; }
        public ServiceProvider Services { get; }
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }

    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = new();

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Commands.Add(command.CommandText);
            return base.ScalarExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
