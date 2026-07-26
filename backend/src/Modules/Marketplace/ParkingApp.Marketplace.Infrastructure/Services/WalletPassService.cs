using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using QRCoder;

namespace ParkingApp.Marketplace.Infrastructure.Services;

/// <summary>
/// Builds Apple Wallet (.pkpass) packages and Google Wallet save JWTs for booking access tokens.
/// </summary>
internal sealed class WalletPassService : IWalletPassService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IOptionsMonitor<WalletPassOptions> _options;
    private readonly ILogger<WalletPassService> _logger;

    public WalletPassService(
        IOptionsMonitor<WalletPassOptions> options,
        ILogger<WalletPassService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public WalletAvailability GetAvailability()
    {
        var o = _options.CurrentValue;
        if (!o.Enabled)
        {
            return new WalletAvailability(
                false,
                false,
                false,
                false,
                "Wallet export is disabled.");
        }

        var appleSigned = o.Apple.HasTeamAndPassType && o.Apple.HasSigningCertificate;
        var appleAvailable = o.Apple.HasTeamAndPassType && (appleSigned || o.AllowUnsignedAppleDownload);
        var googleAvailable = o.Google.IsFullyConfigured;

        string? message = null;
        if (!appleAvailable && !googleAvailable)
        {
            message = "Configure Marketplace:Wallet Apple certs and/or Google issuer credentials to enable device wallets.";
        }
        else if (appleAvailable && !appleSigned)
        {
            message = "Apple Wallet download is available unsigned (dev only). iOS will not install until a Pass Type certificate is configured.";
        }

        return new WalletAvailability(
            true,
            appleAvailable,
            googleAvailable,
            appleSigned,
            message);
    }

    public string? BuildQrDataUrl(string accessToken, int pixels = 280)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(accessToken.Trim(), QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(pixels > 0 ? Math.Clamp(pixels / 25, 4, 20) : 8);
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate QR data URL for access pass");
            return null;
        }
    }

    public AppleWalletPackageResult BuildApplePkPass(WalletPassContent content)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled)
            return FailApple("Wallet export is disabled.");

        if (!o.Apple.HasTeamAndPassType)
            return FailApple("Apple PassTypeIdentifier and TeamIdentifier are required.");

        var canSign = o.Apple.HasSigningCertificate;
        if (!canSign && !o.AllowUnsignedAppleDownload)
            return FailApple("Apple signing certificate is not configured.");

        try
        {
            var passJson = BuildApplePassJson(content, o);
            var passBytes = Encoding.UTF8.GetBytes(passJson);
            var icon = MinimalPng.CreateSolid(29, 29, 15, 23, 42);
            var icon2x = MinimalPng.CreateSolid(58, 58, 15, 23, 42);
            var logo = MinimalPng.CreateSolid(160, 50, 15, 23, 42);

            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["pass.json"] = passBytes,
                ["icon.png"] = icon,
                ["paula.r@example.org"] = icon2x,
                ["logo.png"] = logo
            };

            var manifestObj = new JsonObject();
            foreach (var (name, data) in files)
            {
                var hash = Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
                manifestObj[name] = hash;
            }

            var manifestBytes = Encoding.UTF8.GetBytes(manifestObj.ToJsonString());
            files["manifest.json"] = manifestBytes;

            byte[]? signature = null;
            var isSigned = false;
            if (canSign)
            {
                signature = SignManifest(manifestBytes, o.Apple);
                isSigned = signature is { Length: > 0 };
                if (isSigned)
                    files["signature"] = signature!;
            }

            if (!isSigned && !o.AllowUnsignedAppleDownload)
                return FailApple("Failed to sign Apple Wallet pass.");

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, data) in files)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                    using var stream = entry.Open();
                    stream.Write(data, 0, data.Length);
                }
            }

            var fileName = $"ParkEase-{SanitizeFilePart(content.BookingReference ?? content.BookingId.ToString("N"))}.pkpass";
            return new AppleWalletPackageResult(true, ms.ToArray(), fileName, isSigned, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Apple Wallet pass for booking {BookingId}", content.BookingId);
            return FailApple("Failed to build Apple Wallet pass.");
        }
    }

    public GoogleWalletLinkResult BuildGoogleSaveLink(WalletPassContent content)
    {
        var o = _options.CurrentValue;
        if (!o.Enabled)
            return new GoogleWalletLinkResult(false, null, false, "Wallet export is disabled.");

        if (!o.Google.IsFullyConfigured)
        {
            return new GoogleWalletLinkResult(
                false,
                null,
                false,
                "Google Wallet issuer credentials are not configured.");
        }

        try
        {
            var (email, privateKeyPem) = LoadServiceAccount(o.Google);
            var classId = NormalizeGoogleClassId(o.Google.IssuerId, o.Google.ClassId);
            var objectId = $"{o.Google.IssuerId}.{content.BookingId:N}";

            var now = DateTimeOffset.UtcNow;
            var payload = new Dictionary<string, object?>
            {
                ["iss"] = email,
                ["aud"] = "google",
                ["typ"] = "savetowallet",
                ["iat"] = now.ToUnixTimeSeconds(),
                ["payload"] = new Dictionary<string, object?>
                {
                    ["genericObjects"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = objectId,
                            ["classId"] = classId,
                            ["state"] = "ACTIVE",
                            ["cardTitle"] = Localized(o.OrganizationName + " Access"),
                            ["header"] = Localized(content.BookingReference ?? content.BookingId.ToString("N")[..8].ToUpperInvariant()),
                            ["subheader"] = Localized(Truncate(content.ParkingSpaceTitle, 60)),
                            ["barcode"] = new Dictionary<string, object?>
                            {
                                ["type"] = "QR_CODE",
                                ["value"] = content.AccessToken,
                                ["alternateText"] = content.AccessToken
                            },
                            ["hexBackgroundColor"] = "#0f172a",
                            ["heroImage"] = null,
                            ["textModulesData"] = new object[]
                            {
                                new Dictionary<string, object?>
                                {
                                    ["id"] = "address",
                                    ["header"] = "Address",
                                    ["body"] = Truncate(content.ParkingSpaceAddress, 200)
                                },
                                new Dictionary<string, object?>
                                {
                                    ["id"] = "vehicle",
                                    ["header"] = "Vehicle",
                                    ["body"] = string.IsNullOrWhiteSpace(content.VehicleNumber) ? "—" : content.VehicleNumber
                                }
                            },
                            ["validTimeInterval"] = new Dictionary<string, object?>
                            {
                                ["start"] = new Dictionary<string, object?>
                                {
                                    ["date"] = content.StartDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                                },
                                ["end"] = new Dictionary<string, object?>
                                {
                                    ["date"] = content.EndDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                                }
                            }
                        }
                    }
                }
            };

            var jwt = CreateRs256Jwt(payload, privateKeyPem);
            var saveUrl = "https://pay.google.com/gp/v/save/" + jwt;
            return new GoogleWalletLinkResult(true, saveUrl, true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build Google Wallet link for booking {BookingId}", content.BookingId);
            return new GoogleWalletLinkResult(false, null, true, "Failed to build Google Wallet save link.");
        }
    }

    private static string BuildApplePassJson(WalletPassContent content, WalletPassOptions o)
    {
        var serial = content.BookingId.ToString("N");
        var start = content.StartDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var end = content.EndDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        var title = Truncate(content.ParkingSpaceTitle, 80);
        var address = Truncate(content.ParkingSpaceAddress, 120);
        var vehicle = string.IsNullOrWhiteSpace(content.VehicleNumber) ? "—" : content.VehicleNumber.Trim();
        var bookingRef = string.IsNullOrWhiteSpace(content.BookingReference)
            ? serial[..8].ToUpperInvariant()
            : content.BookingReference.Trim();

        var pass = new Dictionary<string, object?>
        {
            ["formatVersion"] = 1,
            ["passTypeIdentifier"] = o.Apple.PassTypeIdentifier.Trim(),
            ["serialNumber"] = serial,
            ["teamIdentifier"] = o.Apple.TeamIdentifier.Trim(),
            ["organizationName"] = o.OrganizationName,
            ["description"] = "Parking access pass",
            ["logoText"] = o.LogoText,
            ["foregroundColor"] = "rgb(255, 255, 255)",
            ["backgroundColor"] = "rgb(15, 23, 42)",
            ["labelColor"] = "rgb(148, 163, 184)",
            ["relevantDate"] = start,
            ["expirationDate"] = end,
            ["barcode"] = new Dictionary<string, object?>
            {
                ["message"] = content.AccessToken,
                ["format"] = "PKBarcodeFormatQR",
                ["messageEncoding"] = "iso-8859-1",
                ["altText"] = bookingRef
            },
            ["barcodes"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["message"] = content.AccessToken,
                    ["format"] = "PKBarcodeFormatQR",
                    ["messageEncoding"] = "iso-8859-1",
                    ["altText"] = bookingRef
                }
            },
            ["eventTicket"] = new Dictionary<string, object?>
            {
                ["primaryFields"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "location",
                        ["label"] = "LOCATION",
                        ["value"] = title
                    }
                },
                ["secondaryFields"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "start",
                        ["label"] = "START (UTC)",
                        ["value"] = start,
                        ["dateStyle"] = "PKDateStyleMedium",
                        ["timeStyle"] = "PKDateStyleShort",
                        ["ignoresTimeZone"] = true
                    },
                    new Dictionary<string, object?>
                    {
                        ["key"] = "end",
                        ["label"] = "END (UTC)",
                        ["value"] = end,
                        ["dateStyle"] = "PKDateStyleMedium",
                        ["timeStyle"] = "PKDateStyleShort",
                        ["ignoresTimeZone"] = true
                    }
                },
                ["auxiliaryFields"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "ref",
                        ["label"] = "BOOKING",
                        ["value"] = bookingRef
                    },
                    new Dictionary<string, object?>
                    {
                        ["key"] = "vehicle",
                        ["label"] = "VEHICLE",
                        ["value"] = vehicle
                    }
                },
                ["backFields"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["key"] = "address",
                        ["label"] = "Address",
                        ["value"] = address
                    },
                    new Dictionary<string, object?>
                    {
                        ["key"] = "token",
                        ["label"] = "Access token",
                        ["value"] = content.AccessToken
                    },
                    new Dictionary<string, object?>
                    {
                        ["key"] = "help",
                        ["label"] = "Gate staff",
                        ["value"] = "Scan the QR code or verify the token in ParkEase vendor Scan access pass."
                    }
                }
            }
        };

        return JsonSerializer.Serialize(pass, JsonOptions);
    }

    private static byte[] SignManifest(byte[] manifestBytes, AppleWalletOptions apple)
    {
        using var passCert = X509CertificateLoader.LoadPkcs12FromFile(
            apple.CertificatePath!,
            apple.CertificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);

        var contentInfo = new ContentInfo(manifestBytes);
        var signedCms = new SignedCms(contentInfo, detached: true);
        var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, passCert)
        {
            IncludeOption = X509IncludeOption.ExcludeRoot
        };

        if (!string.IsNullOrWhiteSpace(apple.WwdrCertificatePath) && File.Exists(apple.WwdrCertificatePath))
        {
            var wwdr = LoadCertificateFile(apple.WwdrCertificatePath);
            signer.Certificates.Add(wwdr);
        }

        signedCms.ComputeSignature(signer);
        return signedCms.Encode();
    }

    private static X509Certificate2 LoadCertificateFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        // DER or PEM
        try
        {
            return X509CertificateLoader.LoadCertificate(bytes);
        }
        catch
        {
            var text = Encoding.ASCII.GetString(bytes);
            var b64 = ExtractPemBase64(text, "CERTIFICATE");
            return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(b64));
        }
    }

    private static (string Email, string PrivateKeyPem) LoadServiceAccount(GoogleWalletOptions google)
    {
        var json = File.ReadAllText(google.ServiceAccountJsonPath!);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var email = google.ServiceAccountEmail;
        if (string.IsNullOrWhiteSpace(email))
            email = root.GetProperty("client_email").GetString();
        var key = root.GetProperty("private_key").GetString();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Service account JSON must include client_email and private_key.");
        return (email, key);
    }

    private static string CreateRs256Jwt(Dictionary<string, object?> payload, string privateKeyPem)
    {
        var header = new Dictionary<string, object?>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        // Google Wallet JWT uses payload claims at top level (iss, aud, typ, iat, payload)
        var headerJson = JsonSerializer.Serialize(header, JsonOptions);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var headerB64 = Base64Url(Encoding.UTF8.GetBytes(headerJson));
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{headerB64}.{payloadB64}";

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string NormalizeGoogleClassId(string issuerId, string classId)
    {
        var c = classId.Trim();
        if (c.Contains('.', StringComparison.Ordinal))
            return c;
        return $"{issuerId.Trim()}.{c}";
    }

    private static Dictionary<string, object?> Localized(string value) =>
        new()
        {
            ["defaultValue"] = new Dictionary<string, object?>
            {
                ["language"] = "en-US",
                ["value"] = value
            }
        };

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ExtractPemBase64(string pem, string label)
    {
        var begin = $"-----BEGIN {label}-----";
        var end = $"-----END {label}-----";
        var start = pem.IndexOf(begin, StringComparison.Ordinal);
        if (start < 0) throw new InvalidOperationException($"PEM {label} not found.");
        start += begin.Length;
        var stop = pem.IndexOf(end, start, StringComparison.Ordinal);
        if (stop < 0) throw new InvalidOperationException($"PEM {label} end not found.");
        return pem[start..stop].Replace("\r", "").Replace("\n", "").Trim();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }

    private static string SanitizeFilePart(string value)
    {
        var chars = value.Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_').ToArray();
        return chars.Length == 0 ? "pass" : new string(chars);
    }

    private static AppleWalletPackageResult FailApple(string message) =>
        new(false, null, "pass.pkpass", false, message);

    /// <summary>Minimal solid-color PNG writer (no external image dependency for pass icons).</summary>
    private static class MinimalPng
    {
        public static byte[] CreateSolid(int width, int height, byte r, byte g, byte b)
        {
            var raw = new byte[(width * 4 + 1) * height];
            for (var y = 0; y < height; y++)
            {
                var row = y * (width * 4 + 1);
                raw[row] = 0; // filter None
                for (var x = 0; x < width; x++)
                {
                    var i = row + 1 + x * 4;
                    raw[i] = r;
                    raw[i + 1] = g;
                    raw[i + 2] = b;
                    raw[i + 3] = 255;
                }
            }

            var compressed = ZLibCompress(raw);
            using var ms = new MemoryStream();
            // PNG signature
            ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            WriteChunk(ms, "IHDR", BuildIhdr(width, height));
            WriteChunk(ms, "IDAT", compressed);
            WriteChunk(ms, "IEND", Array.Empty<byte>());
            return ms.ToArray();
        }

        private static byte[] BuildIhdr(int width, int height)
        {
            var data = new byte[13];
            WriteInt(data, 0, width);
            WriteInt(data, 4, height);
            data[8] = 8; // bit depth
            data[9] = 6; // RGBA
            return data;
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            WriteIntToStream(stream, data.Length);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            stream.Write(typeBytes);
            stream.Write(data);
            var crcInput = new byte[typeBytes.Length + data.Length];
            Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
            Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
            WriteIntToStream(stream, unchecked((int)Crc32(crcInput)));
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)(value >> 24);
            buffer[offset + 1] = (byte)(value >> 16);
            buffer[offset + 2] = (byte)(value >> 8);
            buffer[offset + 3] = (byte)value;
        }

        private static void WriteIntToStream(Stream stream, int value)
        {
            stream.WriteByte((byte)(value >> 24));
            stream.WriteByte((byte)(value >> 16));
            stream.WriteByte((byte)(value >> 8));
            stream.WriteByte((byte)value);
        }

        private static byte[] ZLibCompress(byte[] data)
        {
            using var output = new MemoryStream();
            // zlib header
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(data, 0, data.Length);
            }

            var adler = Adler32(data);
            output.WriteByte((byte)(adler >> 24));
            output.WriteByte((byte)(adler >> 16));
            output.WriteByte((byte)(adler >> 8));
            output.WriteByte((byte)adler);
            return output.ToArray();
        }

        private static uint Adler32(byte[] data)
        {
            const uint mod = 65521;
            uint a = 1, b = 0;
            foreach (var t in data)
            {
                a = (a + t) % mod;
                b = (b + a) % mod;
            }

            return (b << 16) | a;
        }

        private static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
            {
                crc ^= b;
                for (var i = 0; i < 8; i++)
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }

            return crc ^ 0xFFFFFFFFu;
        }
    }
}
