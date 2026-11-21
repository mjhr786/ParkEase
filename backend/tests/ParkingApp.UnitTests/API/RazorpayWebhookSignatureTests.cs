using System.Text;
using FluentAssertions;
using ParkingApp.Marketplace.Application.Services;
using Xunit;

namespace ParkingApp.UnitTests.API;

public class RazorpayWebhookSignatureTests
{
    private const string Secret = "whsec_test";

    [Fact]
    public void ComputeHex_IsLowercaseHmacSha256()
    {
        var body = Encoding.UTF8.GetBytes("""{"event":"payment.captured"}""");
        var hex = RazorpayWebhookSignature.ComputeHex(body, Secret);

        hex.Should().MatchRegex("^[0-9a-f]{64}$");
        RazorpayWebhookSignature.FixedTimeEqualsHex(hex, hex).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zz")]
    public void TryNormalizeSignature_RejectsMissingOddAndNonHex(string? header)
    {
        RazorpayWebhookSignature.TryNormalizeSignature(header, out _).Should().BeFalse();
    }

    [Fact]
    public void TryNormalizeSignature_LowercasesValidHex()
    {
        RazorpayWebhookSignature.TryNormalizeSignature("AB", out var normalized).Should().BeTrue();
        normalized.Should().Be("ab");
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/json;charset=utf-8")]
    [InlineData("Application/JSON")]
    public void IsJsonContentType_AcceptsJson(string contentType)
    {
        RazorpayWebhookSignature.IsJsonContentType(contentType).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/json; charset=utf-16")]
    public void IsJsonContentType_RejectsOtherTypes(string? contentType)
    {
        RazorpayWebhookSignature.IsJsonContentType(contentType).Should().BeFalse();
    }

    [Fact]
    public void FallbackEventId_DoesNotIncludeCreatedAt()
    {
        var a = RazorpayWebhookSignature.FallbackEventId("razorpay", "payment.captured", "pay_1");
        var b = RazorpayWebhookSignature.FallbackEventId("razorpay", "payment.captured", "pay_1");
        var other = RazorpayWebhookSignature.FallbackEventId("razorpay", "order.paid", "pay_1");

        a.Should().Be(b);
        a.Should().StartWith("rzp_fb_");
        a.Should().NotBe(other);
        a.Length.Should().BeLessThanOrEqualTo(128);
    }

    [Fact]
    public void FixedTimeEqualsHex_DifferentLength_IsFalse()
    {
        RazorpayWebhookSignature.FixedTimeEqualsHex("aa", "aabb").Should().BeFalse();
    }
}
