using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Npgsql;

namespace ParkingApp.IntegrationTests.Outbox;

/// <summary>
/// Local TCP proxy that speaks enough of the PostgreSQL v3 protocol to drop one
/// claim response. Scenario B reads the server bytes through ReadyForQuery, checks
/// the row on a second connection, and only then closes the client socket.
/// The claim bytes are not forwarded back. This is not an exception thrown before commit.
/// </summary>
internal sealed class OutboxResponseLossProxy : IAsyncDisposable
{
    internal enum LossMode
    {
        Transparent = 0,
        FailBeforeCommit = 1,
        LoseCommittedResponse = 2
    }

    internal sealed record CycleObservation(
        int ConnectionId,
        long ElapsedMs,
        string ClientMessageTypes,
        string ServerMessageTypes,
        string Sql,
        string CommandTags,
        string? ReadyStatus,
        string Action,
        int? ObservedStatus,
        int? ObservedAttempt,
        int ObservedRowCount,
        string? ObserverError);

    private readonly record struct PgMessage(byte Type, byte[] Payload);

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _dispose = new();
    private readonly string _upstreamHost;
    private readonly int _upstreamPort;
    private readonly string _observerConnectionString;
    private readonly LossMode _mode;
    private readonly long _started;
    private readonly object _gate = new();
    private readonly List<CycleObservation> _cycles = new();
    private readonly List<string> _log = new();
    private readonly Task _acceptLoop;
    private int _dropsLeft;
    private int _connectionIds;

    private OutboxResponseLossProxy(
        TcpListener listener,
        int port,
        string upstreamHost,
        int upstreamPort,
        string observerConnectionString,
        LossMode mode,
        int maxDrops)
    {
        _listener = listener;
        Port = port;
        _upstreamHost = upstreamHost;
        _upstreamPort = upstreamPort;
        _observerConnectionString = observerConnectionString;
        _mode = mode;
        _dropsLeft = maxDrops;
        _started = System.Diagnostics.Stopwatch.GetTimestamp();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    public IReadOnlyList<CycleObservation> Cycles
    {
        get
        {
            lock (_gate)
                return _cycles.ToList();
        }
    }

    public IReadOnlyList<string> Log
    {
        get
        {
            lock (_gate)
                return _log.ToList();
        }
    }

    public static Task<OutboxResponseLossProxy> StartAsync(
        string upstreamHost,
        int upstreamPort,
        string observerConnectionString,
        LossMode mode,
        int maxDrops)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var proxy = new OutboxResponseLossProxy(
            listener,
            ((IPEndPoint)listener.LocalEndpoint).Port,
            upstreamHost,
            upstreamPort,
            observerConnectionString,
            mode,
            maxDrops);
        proxy.AddLog(
            $"listening 127.0.0.1:{proxy.Port} -> {upstreamHost}:{upstreamPort} mode={mode} maxDrops={maxDrops}");
        return Task.FromResult(proxy);
    }

    public string FormatTrace()
    {
        var builder = new StringBuilder();
        foreach (var line in Log)
            builder.AppendLine(line);
        foreach (var cycle in Cycles)
        {
            builder.Append("cycle conn=").Append(cycle.ConnectionId)
                .Append(" t=").Append(cycle.ElapsedMs).Append("ms")
                .Append(" action=").Append(cycle.Action)
                .Append(" ready=").Append(cycle.ReadyStatus ?? "-")
                .Append(" tags=[").Append(cycle.CommandTags).Append(']')
                .Append(" clientTypes=").Append(cycle.ClientMessageTypes)
                .Append(" serverTypes=").Append(cycle.ServerMessageTypes)
                .Append(" observedCount=").Append(cycle.ObservedRowCount)
                .Append(" observedStatus=").Append(cycle.ObservedStatus?.ToString() ?? "-")
                .Append(" observedAttempt=").Append(cycle.ObservedAttempt?.ToString() ?? "-");
            if (!string.IsNullOrEmpty(cycle.ObserverError))
                builder.Append(" observerError=").Append(cycle.ObserverError);
            builder.AppendLine();
            builder.AppendLine(cycle.Sql);
        }

        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        _dispose.Cancel();
        _listener.Stop();
        try
        {
            await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // The accept loop ends when the listener stops.
        }

        _dispose.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_dispose.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_dispose.Token);
            }
            catch
            {
                break;
            }

            var id = Interlocked.Increment(ref _connectionIds);
            _ = Task.Run(() => HandleAsync(id, client));
        }
    }

    private async Task HandleAsync(int connectionId, TcpClient client)
    {
        client.NoDelay = true;
        TcpClient? upstream = null;
        try
        {
            upstream = new TcpClient { NoDelay = true };
            await upstream.ConnectAsync(_upstreamHost, _upstreamPort, _dispose.Token);
            await using var clientStream = client.GetStream();
            await using var serverStream = upstream.GetStream();
            AddLog($"conn {connectionId} open");

            await RelayStartupAsync(connectionId, clientStream, serverStream);

            var incoming = Channel.CreateUnbounded<PgMessage>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
            var pump = Task.Run(() => PumpClientAsync(connectionId, clientStream, incoming.Writer));
            try
            {
                await AuthenticateAsync(connectionId, serverStream, clientStream, incoming.Reader);
                await CommandLoopAsync(connectionId, serverStream, clientStream, incoming.Reader);
            }
            finally
            {
                incoming.Writer.TryComplete();
                try
                {
                    client.Close();
                }
                catch
                {
                    // Closing the client is how a dropped response is delivered.
                }

                try
                {
                    await pump.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                    // The pump ends when the client socket closes.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException or SocketException)
        {
            AddLog($"conn {connectionId} ended: {ex.GetType().Name}: {ex.Message}");
        }
        catch (Exception ex)
        {
            AddLog($"conn {connectionId} failed: {ex}");
        }
        finally
        {
            client.Dispose();
            upstream?.Dispose();
            AddLog($"conn {connectionId} closed");
        }
    }

    private async Task RelayStartupAsync(int connectionId, NetworkStream clientStream, NetworkStream serverStream)
    {
        var first = await ReadPrefixedAsync(clientStream, _dispose.Token);
        var code = BinaryPrimitives.ReadInt32BigEndian(first.AsSpan(4));
        if (first.Length == 8 && code == 80877103)
        {
            await serverStream.WriteAsync(first, _dispose.Token);
            var answer = await ReadExactAsync(serverStream, 1, _dispose.Token);
            await clientStream.WriteAsync(answer, _dispose.Token);
            if (answer[0] != (byte)'N')
                throw new InvalidOperationException(
                    $"PostgreSQL requested SSL ({(char)answer[0]}). This proxy only relays a plaintext test connection.");

            first = await ReadPrefixedAsync(clientStream, _dispose.Token);
            code = BinaryPrimitives.ReadInt32BigEndian(first.AsSpan(4));
            AddLog($"conn {connectionId} SSL declined by server");
        }

        if (code != 196608)
            throw new InvalidOperationException($"Unexpected startup code {code} on connection {connectionId}.");

        await serverStream.WriteAsync(first, _dispose.Token);
    }

    private async Task AuthenticateAsync(
        int connectionId,
        NetworkStream serverStream,
        NetworkStream clientStream,
        ChannelReader<PgMessage> clientMessages)
    {
        while (true)
        {
            using var budget = ReadBudget();
            var message = await ReadTypedAsync(serverStream, budget.Token);
            await WriteMessageAsync(clientStream, message, _dispose.Token);
            if (message.Type == (byte)'Z')
            {
                AddLog($"conn {connectionId} authenticated");
                return;
            }

            if (message.Type == (byte)'E')
                throw new InvalidOperationException(
                    $"Authentication failed on connection {connectionId}: {DescribeError(message)}");

            if (message.Type != (byte)'R' || message.Payload.Length < 4)
                continue;

            var authCode = BinaryPrimitives.ReadInt32BigEndian(message.Payload);
            if (authCode is 3 or 5 or 10 or 11)
            {
                var reply = await clientMessages.ReadAsync(_dispose.Token);
                await WriteMessageAsync(serverStream, reply, _dispose.Token);
            }
            else if (authCode is not (0 or 12))
            {
                throw new InvalidOperationException(
                    $"Unsupported PostgreSQL auth code {authCode} on connection {connectionId}.");
            }
        }
    }

    private async Task CommandLoopAsync(
        int connectionId,
        NetworkStream serverStream,
        NetworkStream clientStream,
        ChannelReader<PgMessage> clientMessages)
    {
        var uncommittedClaim = false;
        while (!_dispose.IsCancellationRequested)
        {
            var batch = await ReadClientBatchAsync(clientMessages);
            if (batch == null)
                return;

            var sqls = ExtractSql(batch);
            var sql = string.Join("\n", sqls);
            var claim = sqls.Exists(IsClaimUpdate);
            var commit = sqls.Exists(IsCommit);
            var clientTypes = Types(batch);

            if (batch[^1].Type == (byte)'X')
            {
                await WriteAllAsync(serverStream, batch, _dispose.Token);
                return;
            }

            if (_mode == LossMode.FailBeforeCommit && claim && TryConsumeDrop())
            {
                var observed = await ObserveAsync();
                Record(connectionId, clientTypes, "", sql, "", null, "DroppedBeforeForward", observed);
                AddLog($"conn {connectionId} dropped claim before the server received it");
                return;
            }

            await WriteAllAsync(serverStream, batch, _dispose.Token);
            var response = await ReadServerUntilReadyAsync(serverStream);
            var tags = ExtractTags(response);
            var tagText = string.Join(", ", tags);
            var ready = ReadyStatus(response);
            var serverTypes = Types(response);
            var hasError = response.Exists(m => m.Type == (byte)'E');
            var hasUpdate = tags.Exists(t => t.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase));
            var hasRollback = tags.Exists(t => t.StartsWith("ROLLBACK", StringComparison.OrdinalIgnoreCase));
            var hasCommitTag = tags.Exists(t => t.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase));

            var thisBatchCommittedClaim = claim && ready == 'I' && hasUpdate && !hasError && !hasRollback;
            var laterCommit = uncommittedClaim && ready == 'I' && (commit || hasCommitTag) && !hasError && !hasRollback;

            if (claim && ready == 'T' && hasUpdate && !hasError)
                uncommittedClaim = true;

            if ((thisBatchCommittedClaim || laterCommit)
                && _mode == LossMode.LoseCommittedResponse
                && TryConsumeDrop())
            {
                var observed = await ObserveAsync();
                Record(connectionId, clientTypes, serverTypes, sql, tagText, ready, "DroppedAfterCommit", observed);
                AddLog(
                    $"conn {connectionId} withheld a committed response ready={ready} tags=[{tagText}] observed={FormatObserved(observed)}");
                return;
            }

            if (thisBatchCommittedClaim || (uncommittedClaim && ready == 'I' && (hasCommitTag || hasRollback || commit)))
                uncommittedClaim = false;

            Snapshot? observedForward = null;
            if (claim && hasUpdate && ready == 'I' && !hasError)
                observedForward = await ObserveAsync();

            await WriteAllAsync(clientStream, response, _dispose.Token);
            Record(connectionId, clientTypes, serverTypes, sql, tagText, ready, "Forwarded", observedForward);
        }
    }

    private async Task<List<PgMessage>?> ReadClientBatchAsync(ChannelReader<PgMessage> reader)
    {
        try
        {
            if (!await reader.WaitToReadAsync(_dispose.Token))
                return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var batch = new List<PgMessage>();
        while (true)
        {
            using var budget = ReadBudget();
            var message = await reader.ReadAsync(budget.Token);
            batch.Add(message);
            if (message.Type is (byte)'S' or (byte)'Q' or (byte)'X')
                return batch;

            if (message.Type != (byte)'H')
                continue;

            using var peek = CancellationTokenSource.CreateLinkedTokenSource(_dispose.Token);
            peek.CancelAfter(TimeSpan.FromMilliseconds(200));
            try
            {
                if (!await reader.WaitToReadAsync(peek.Token))
                    return batch;
            }
            catch (OperationCanceledException) when (!_dispose.IsCancellationRequested)
            {
                return batch;
            }
        }
    }

    private async Task PumpClientAsync(int connectionId, NetworkStream clientStream, ChannelWriter<PgMessage> writer)
    {
        try
        {
            while (!_dispose.IsCancellationRequested)
            {
                var message = await ReadTypedAsync(clientStream, _dispose.Token);
                await writer.WriteAsync(message, _dispose.Token);
            }
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or OperationCanceledException or SocketException)
        {
            AddLog($"conn {connectionId} client pump ended: {ex.GetType().Name}");
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<List<PgMessage>> ReadServerUntilReadyAsync(NetworkStream serverStream)
    {
        var response = new List<PgMessage>();
        while (true)
        {
            using var budget = ReadBudget();
            var message = await ReadTypedAsync(serverStream, budget.Token);
            response.Add(message);
            if (message.Type == (byte)'Z')
                return response;
        }
    }

    private bool TryConsumeDrop() => Interlocked.Decrement(ref _dropsLeft) >= 0;

    private async Task<Snapshot?> ObserveAsync()
    {
        try
        {
            await using var connection = new NpgsqlConnection(_observerConnectionString);
            await connection.OpenAsync(_dispose.Token);
            await using var command = new NpgsqlCommand(
                """SELECT "Status", "AttemptCount" FROM "OutboxMessages" """,
                connection);
            command.CommandTimeout = 5;
            await using var reader = await command.ExecuteReaderAsync(_dispose.Token);
            var count = 0;
            var status = 0;
            var attempt = 0;
            while (await reader.ReadAsync(_dispose.Token))
            {
                count++;
                status = reader.GetInt32(0);
                attempt = reader.GetInt32(1);
            }

            return new Snapshot(status, attempt, count, null);
        }
        catch (Exception ex)
        {
            return new Snapshot(null, null, 0, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private void Record(
        int connectionId,
        string clientTypes,
        string serverTypes,
        string sql,
        string tags,
        char? ready,
        string action,
        Snapshot? observed)
    {
        var cycle = new CycleObservation(
            connectionId,
            (long)System.Diagnostics.Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
            clientTypes,
            serverTypes,
            sql,
            tags,
            ready?.ToString(),
            action,
            observed?.Status,
            observed?.Attempt,
            observed?.Count ?? 0,
            observed?.Error);
        lock (_gate)
            _cycles.Add(cycle);
    }

    private void AddLog(string message)
    {
        lock (_gate)
            _log.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {message}");
    }

    private CancellationTokenSource ReadBudget()
    {
        var budget = CancellationTokenSource.CreateLinkedTokenSource(_dispose.Token);
        budget.CancelAfter(TimeSpan.FromSeconds(20));
        return budget;
    }

    private static async Task<byte[]> ReadPrefixedAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var lengthBytes = await ReadExactAsync(stream, 4, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(lengthBytes);
        if (length < 8 || length > 100_000)
            throw new InvalidOperationException($"Invalid startup length {length}.");

        var rest = await ReadExactAsync(stream, length - 4, cancellationToken);
        var message = new byte[length];
        lengthBytes.CopyTo(message, 0);
        rest.CopyTo(message, 4);
        return message;
    }

    private static async Task<PgMessage> ReadTypedAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(stream, 5, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(1, 4));
        if (length < 4 || length > 16_000_000)
            throw new InvalidOperationException($"Invalid message length {length} type {(char)header[0]}.");

        var payload = length == 4
            ? Array.Empty<byte>()
            : await ReadExactAsync(stream, length - 4, cancellationToken);
        return new PgMessage(header[0], payload);
    }

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken cancellationToken)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), cancellationToken);
            if (read == 0)
                throw new EndOfStreamException($"Expected {count} bytes and received {offset}.");
            offset += read;
        }

        return buffer;
    }

    private static async Task WriteAllAsync(
        NetworkStream stream,
        IReadOnlyList<PgMessage> messages,
        CancellationToken cancellationToken)
    {
        foreach (var message in messages)
            await WriteMessageAsync(stream, message, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static Task WriteMessageAsync(NetworkStream stream, PgMessage message, CancellationToken cancellationToken)
    {
        var length = message.Payload.Length + 4;
        var buffer = new byte[5 + message.Payload.Length];
        buffer[0] = message.Type;
        BinaryPrimitives.WriteInt32BigEndian(buffer.AsSpan(1, 4), length);
        message.Payload.CopyTo(buffer.AsSpan(5));
        return stream.WriteAsync(buffer, cancellationToken).AsTask();
    }

    private static List<string> ExtractSql(IReadOnlyList<PgMessage> batch)
    {
        var sql = new List<string>();
        foreach (var message in batch)
        {
            if (message.Type == (byte)'Q')
            {
                sql.Add(ReadCString(message.Payload, 0));
            }
            else if (message.Type == (byte)'P')
            {
                var offset = 0;
                _ = ReadCString(message.Payload, ref offset);
                sql.Add(ReadCString(message.Payload, ref offset));
            }
        }

        return sql;
    }

    private static List<string> ExtractTags(IReadOnlyList<PgMessage> response)
    {
        var tags = new List<string>();
        foreach (var message in response)
        {
            if (message.Type == (byte)'C')
                tags.Add(Encoding.UTF8.GetString(message.Payload).TrimEnd('\0'));
        }

        return tags;
    }

    private static char? ReadyStatus(IReadOnlyList<PgMessage> response)
    {
        for (var i = response.Count - 1; i >= 0; i--)
        {
            if (response[i].Type == (byte)'Z' && response[i].Payload.Length > 0)
                return (char)response[i].Payload[0];
        }

        return null;
    }

    private static string Types(IReadOnlyList<PgMessage> messages) =>
        string.Concat(messages.Select(message => (char)message.Type));

    private static string ReadCString(byte[] payload, int start)
    {
        var offset = start;
        return ReadCString(payload, ref offset);
    }

    private static string ReadCString(byte[] payload, ref int offset)
    {
        var start = offset;
        while (offset < payload.Length && payload[offset] != 0)
            offset++;

        var text = Encoding.UTF8.GetString(payload, start, offset - start);
        if (offset < payload.Length)
            offset++;
        return text;
    }

    private static bool IsClaimUpdate(string sql)
    {
        if (!sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            || !sql.Contains("OutboxMessages", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var compact = sql.Replace(" ", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\t", "", StringComparison.Ordinal);
        return compact.Contains("AttemptCount\"+1", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("AttemptCount+1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCommit(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeError(PgMessage message)
    {
        var text = new StringBuilder();
        var offset = 0;
        while (offset < message.Payload.Length)
        {
            var field = (char)message.Payload[offset];
            offset++;
            if (field == '\0')
                break;
            var value = ReadCString(message.Payload, ref offset);
            text.Append(field).Append('=').Append(value).Append(' ');
        }

        return text.ToString();
    }

    private static string FormatObserved(Snapshot? snapshot) =>
        snapshot == null
            ? "-"
            : $"count={snapshot.Count} status={snapshot.Status} attempt={snapshot.Attempt} error={snapshot.Error}";

    private sealed record Snapshot(int? Status, int? Attempt, int Count, string? Error);
}
