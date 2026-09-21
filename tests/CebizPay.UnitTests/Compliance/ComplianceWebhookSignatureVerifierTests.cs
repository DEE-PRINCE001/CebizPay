#pragma warning disable CS1591
using System.Security.Cryptography;
using System.Text;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class ComplianceWebhookSignatureVerifierTests
{
    private readonly ComplianceWebhookSignatureVerifier _verifier = new(NullLogger<ComplianceWebhookSignatureVerifier>.Instance);

    [Fact]
    public void VerifySignature_DojahValidHmacSha256_ReturnsTrue()
    {
        const string secret = "dojah_wh_secret_xyz";
        const string payload = "{\"event\":\"verification.completed\",\"data\":{\"id\":\"123\"}}";

        var hash = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var headers = new Dictionary<string, string>
        {
            { "x-dojah-signature", hash }
        };

        var result = _verifier.VerifySignature(VerificationProvider.Dojah, payload, headers, secret);
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_DojahValidV2Sha256_ReturnsTrue()
    {
        const string secret = "dojah_wh_secret_xyz";
        const string payload = "{\"event\":\"verification.completed\",\"data\":{\"id\":\"123\"}}";

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

        var headers = new Dictionary<string, string>
        {
            { "x-dojah-signature-v2", hash }
        };

        var result = _verifier.VerifySignature(VerificationProvider.Dojah, payload, headers, secret);
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_DojahBothHeadersSupplied_WhenV2Valid_ReturnsTrue()
    {
        const string secret = "dojah_wh_secret_xyz";
        const string payload = "{\"event\":\"verification.completed\",\"data\":{\"id\":\"123\"}}";

        var v2Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

        // Simulate scenario where x-dojah-signature (V1) is present (even if mismatched due to whitespace), but x-dojah-signature-v2 matches
        var headers = new Dictionary<string, string>
        {
            { "X-Dojah-Signature", "some_differing_v1_hash" },
            { "X-Dojah-Signature-V2", v2Hash }
        };

        var result = _verifier.VerifySignature(VerificationProvider.Dojah, payload, headers, secret);
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_DojahInvalidSignature_ReturnsFalse()
    {
        const string secret = "dojah_wh_secret_xyz";
        const string payload = "{\"event\":\"verification.completed\"}";

        var headers = new Dictionary<string, string>
        {
            { "x-dojah-signature", "invalid_signature_hex" }
        };

        var result = _verifier.VerifySignature(VerificationProvider.Dojah, payload, headers, secret);
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_SmileIdValidHmacSha256_ReturnsTrue()
    {
        const string secret = "smile_api_key_123";
        const string payload = "{\"JobId\":\"smile_job_999\",\"ResultCode\":\"0810\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBase64 = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));

        var headers = new Dictionary<string, string>
        {
            { "X-Smile-Signature", signatureBase64 }
        };

        var result = _verifier.VerifySignature(VerificationProvider.SmileId, payload, headers, secret);
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_NinjaValidHmacSha256_ReturnsTrue()
    {
        const string secret = "ninja_secret_456";
        const string payload = "{\"event\":\"kyc.verified\",\"id\":\"ninja_event_01\"}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

        var headers = new Dictionary<string, string>
        {
            { "X-Ninja-Signature", hash }
        };

        var result = _verifier.VerifySignature(VerificationProvider.Ninja, payload, headers, secret);
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_MissingSignatureHeader_ReturnsFalse()
    {
        var headers = new Dictionary<string, string>();
        var result = _verifier.VerifySignature(VerificationProvider.Dojah, "{}", headers, "secret");

        Assert.False(result);
    }
}
