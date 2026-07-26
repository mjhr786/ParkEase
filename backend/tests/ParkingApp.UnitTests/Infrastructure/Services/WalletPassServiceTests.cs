using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ParkingApp.Marketplace.Application.Interfaces;
using ParkingApp.Marketplace.Application.Options;
using ParkingApp.Marketplace.Infrastructure.Services;
using Xunit;

namespace ParkingApp.UnitTests.Infrastructure.Services;

public class WalletPassServiceTests
{
    private static WalletPassContent SampleContent() =>
        new(
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            "BKTEST99",
            "PE-BKTEST99-ABCDEF0123456789",
            "Downtown Garage",
            "100 Main St",
            new DateTime(2026, 7, 25, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 25, 14, 0, 0, DateTimeKind.Utc),
            "KA01AB1234");

    private static WalletPassService CreateService(WalletPassOptions options)
    {
        var monitor = new StaticOptionsMonitor<WalletPassOptions>(options);
        return new WalletPassService(monitor, NullLogger<WalletPassService>.Instance);
    }

    [Fact]
    public void GetAvailability_Disabled_ReportsUnavailable()
    {
        var svc = CreateService(new WalletPassOptions { Enabled = false });
        var a = svc.GetAvailability();
        a.Enabled.Should().BeFalse();
        a.AppleWalletAvailable.Should().BeFalse();
        a.GoogleWalletAvailable.Should().BeFalse();
    }

    [Fact]
    public void GetAvailability_UnsignedAppleAllowed_IsAvailable()
    {
        var svc = CreateService(new WalletPassOptions
        {
            Enabled = true,
            AllowUnsignedAppleDownload = true,
            Apple = new AppleWalletOptions
            {
                PassTypeIdentifier = "pass.com.parkease.access",
                TeamIdentifier = "DEVELOPMENT"
            }
        });

        var a = svc.GetAvailability();
        a.AppleWalletAvailable.Should().BeTrue();
        a.AppleIsSigned.Should().BeFalse();
        a.GoogleWalletAvailable.Should().BeFalse();
    }

    [Fact]
    public void BuildQrDataUrl_ReturnsPngDataUri()
    {
        var svc = CreateService(new WalletPassOptions());
        var url = svc.BuildQrDataUrl("PE-TEST-TOKEN");
        url.Should().NotBeNull();
        url!.Should().StartWith("data:image/png;base64,");
        url.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void BuildApplePkPass_Unsigned_ContainsPassJsonWithBarcodeToken()
    {
        var svc = CreateService(new WalletPassOptions
        {
            Enabled = true,
            AllowUnsignedAppleDownload = true,
            OrganizationName = "ParkEase",
            Apple = new AppleWalletOptions
            {
                PassTypeIdentifier = "pass.com.parkease.access",
                TeamIdentifier = "DEVELOPMENT"
            }
        });

        var result = svc.BuildApplePkPass(SampleContent());
        result.Success.Should().BeTrue();
        result.IsSigned.Should().BeFalse();
        result.Content.Should().NotBeNull();
        result.FileName.Should().EndWith(".pkpass");

        using var zip = new ZipArchive(new MemoryStream(result.Content!), ZipArchiveMode.Read);
        zip.GetEntry("pass.json").Should().NotBeNull();
        zip.GetEntry("manifest.json").Should().NotBeNull();
        zip.GetEntry("icon.png").Should().NotBeNull();
        zip.GetEntry("signature").Should().BeNull();

        using var passStream = zip.GetEntry("pass.json")!.Open();
        using var reader = new StreamReader(passStream, Encoding.UTF8);
        var passJson = reader.ReadToEnd();
        using var doc = JsonDocument.Parse(passJson);
        var root = doc.RootElement;
        root.GetProperty("passTypeIdentifier").GetString().Should().Be("pass.com.parkease.access");
        root.GetProperty("teamIdentifier").GetString().Should().Be("DEVELOPMENT");
        root.GetProperty("barcode").GetProperty("message").GetString()
            .Should().Be("PE-BKTEST99-ABCDEF0123456789");
        root.GetProperty("serialNumber").GetString()
            .Should().Be("aaaaaaaabbbbccccddddeeeeeeeeeeee");
    }

    [Fact]
    public void BuildApplePkPass_WithoutUnsignedFlag_FailsWhenNoCert()
    {
        var svc = CreateService(new WalletPassOptions
        {
            Enabled = true,
            AllowUnsignedAppleDownload = false,
            Apple = new AppleWalletOptions
            {
                PassTypeIdentifier = "pass.com.parkease.access",
                TeamIdentifier = "TEAM123456"
            }
        });

        var result = svc.BuildApplePkPass(SampleContent());
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void BuildGoogleSaveLink_Unconfigured_FailsClearly()
    {
        var svc = CreateService(new WalletPassOptions
        {
            Enabled = true,
            Google = new GoogleWalletOptions()
        });

        var result = svc.BuildGoogleSaveLink(SampleContent());
        result.Success.Should().BeFalse();
        result.IsConfigured.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not configured");
    }

    [Fact]
    public void BuildGoogleSaveLink_WithServiceAccount_ReturnsSaveUrlJwt()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();

        var saPath = Path.Combine(Path.GetTempPath(), $"parkease-sa-{Guid.NewGuid():N}.json");
        try
        {
            var sa = new
            {
                type = "service_account",
                client_email = "wallet-sa@example.iam.gserviceaccount.com",
                private_key = privatePem
            };
            File.WriteAllText(saPath, JsonSerializer.Serialize(sa));

            var svc = CreateService(new WalletPassOptions
            {
                Enabled = true,
                OrganizationName = "ParkEase",
                Google = new GoogleWalletOptions
                {
                    IssuerId = "3388000000000000000",
                    ClassId = "parkease_access",
                    ServiceAccountJsonPath = saPath
                }
            });

            var result = svc.BuildGoogleSaveLink(SampleContent());
            result.Success.Should().BeTrue(result.ErrorMessage);
            result.SaveUrl.Should().StartWith("https://pay.google.com/gp/v/save/");
            var jwt = result.SaveUrl!["https://pay.google.com/gp/v/save/".Length..];
            jwt.Split('.').Should().HaveCount(3);

            // Decode payload (middle segment) and assert barcode token
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(jwt.Split('.')[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            doc.RootElement.GetProperty("iss").GetString()
                .Should().Be("wallet-sa@example.iam.gserviceaccount.com");
            doc.RootElement.GetProperty("aud").GetString().Should().Be("google");
            var barcode = doc.RootElement
                .GetProperty("payload")
                .GetProperty("genericObjects")[0]
                .GetProperty("barcode")
                .GetProperty("value")
                .GetString();
            barcode.Should().Be("PE-BKTEST99-ABCDEF0123456789");
        }
        finally
        {
            if (File.Exists(saPath))
                File.Delete(saPath);
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
        where T : class
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
