using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using ParkingApp.Corporate.Application.Interfaces;

namespace ParkingApp.Corporate.Infrastructure.Services;

/// <summary>
/// Minimal public DNS TXT resolver (UDP, system DNS server). No third-party package.
/// Prefer host <c>_parkease-sso.{domain}</c>; also used for apex fallback.
/// </summary>
public sealed class DnsTxtLookup : IDnsTxtLookup
{
    private static readonly IPEndPoint[] DefaultServers =
    {
        new(IPAddress.Parse("8.8.8.8"), 53),
        new(IPAddress.Parse("1.1.1.1"), 53)
    };

    private readonly ILogger<DnsTxtLookup> _logger;

    public DnsTxtLookup(ILogger<DnsTxtLookup> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> LookupTxtAsync(
        string host,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host))
            return Array.Empty<string>();

        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        try
        {
            var query = BuildTxtQuery(normalized);
            foreach (var server in DefaultServers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await QueryUdpAsync(server, query, cancellationToken);
                if (response is null || response.Length < 12)
                    continue;

                var records = ParseTxtAnswers(response, normalized);
                if (records.Count > 0)
                    return records;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "DNS TXT lookup failed for {Host}", normalized);
        }

        return Array.Empty<string>();
    }

    private static async Task<byte[]?> QueryUdpAsync(
        IPEndPoint server,
        byte[] query,
        CancellationToken cancellationToken)
    {
        using var udp = new UdpClient();
        udp.Client.ReceiveTimeout = 5000;
        udp.Client.SendTimeout = 5000;
        await udp.SendAsync(query, query.Length, server);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var result = await udp.ReceiveAsync(cts.Token);
            return result.Buffer;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static byte[] BuildTxtQuery(string host)
    {
        // Header: ID + flags (standard query) + QDCOUNT=1
        var id = (ushort)Random.Shared.Next(1, ushort.MaxValue);
        using var ms = new MemoryStream();
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16BigEndian(header[..2], id);
        header[2] = 0x01; // RD
        header[3] = 0x00;
        BinaryPrimitives.WriteUInt16BigEndian(header[4..6], 1); // QDCOUNT
        BinaryPrimitives.WriteUInt16BigEndian(header[6..8], 0);
        BinaryPrimitives.WriteUInt16BigEndian(header[8..10], 0);
        BinaryPrimitives.WriteUInt16BigEndian(header[10..12], 0);
        ms.Write(header);

        foreach (var label in host.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length > 63)
                throw new ArgumentException("DNS label too long", nameof(host));
            ms.WriteByte((byte)bytes.Length);
            ms.Write(bytes);
        }
        ms.WriteByte(0); // root
        Span<byte> qtype = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(qtype[..2], 16); // TXT
        BinaryPrimitives.WriteUInt16BigEndian(qtype[2..4], 1); // IN
        ms.Write(qtype);
        return ms.ToArray();
    }

    private static IReadOnlyList<string> ParseTxtAnswers(byte[] response, string expectedHost)
    {
        if (response.Length < 12)
            return Array.Empty<string>();

        var qdCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(4, 2));
        var anCount = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(6, 2));
        var offset = 12;

        // Skip questions
        for (var i = 0; i < qdCount; i++)
        {
            if (!SkipName(response, ref offset))
                return Array.Empty<string>();
            offset += 4; // type + class
            if (offset > response.Length)
                return Array.Empty<string>();
        }

        var results = new List<string>();
        for (var i = 0; i < anCount; i++)
        {
            if (!SkipName(response, ref offset))
                break;
            if (offset + 10 > response.Length)
                break;

            var type = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
            offset += 2;
            offset += 2; // class
            offset += 4; // TTL
            var rdLength = BinaryPrimitives.ReadUInt16BigEndian(response.AsSpan(offset, 2));
            offset += 2;
            if (offset + rdLength > response.Length)
                break;

            if (type == 16) // TXT
            {
                var end = offset + rdLength;
                var pos = offset;
                var sb = new StringBuilder();
                while (pos < end)
                {
                    var len = response[pos++];
                    if (pos + len > end)
                        break;
                    sb.Append(Encoding.UTF8.GetString(response, pos, len));
                    pos += len;
                }
                if (sb.Length > 0)
                    results.Add(sb.ToString());
            }

            offset += rdLength;
        }

        _ = expectedHost; // reserved for future name validation
        return results;
    }

    private static bool SkipName(byte[] message, ref int offset)
    {
        var jumps = 0;
        while (offset < message.Length)
        {
            var len = message[offset];
            if (len == 0)
            {
                offset++;
                return true;
            }

            // Compression pointer
            if ((len & 0xC0) == 0xC0)
            {
                offset += 2;
                return true;
            }

            offset += 1 + len;
            if (++jumps > 50)
                return false;
        }

        return false;
    }
}
