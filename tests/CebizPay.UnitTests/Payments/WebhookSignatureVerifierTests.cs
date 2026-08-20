using System.Security.Cryptography;
using System.Text;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="WebhookSignatureVerifier"/> verifying constant-time cryptographic validation.
/// </summary>
public sealed class WebhookSignatureVerifierTests
{
    private readonly WebhookSignatureVerifier _verifier = new();

    [Fact]
    public void VerifySignature_FlutterwaveValidVerifHash_ShouldReturnTrue()
    {
        // Arrange
        const string secretHash = "flw_webhook_secret_hash_9876";
        const string payload = """{"event":"charge.completed","data":{"id":12345}}""";
        var headers = new Dictionary<string, string>
        {
            { "verif-hash", secretHash }
        };

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Flutterwave, payload, headers, secretHash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_FlutterwaveInvalidVerifHash_ShouldReturnFalse()
    {
        // Arrange
        const string secretHash = "flw_webhook_secret_hash_9876";
        const string payload = """{"event":"charge.completed","data":{"id":12345}}""";
        var headers = new Dictionary<string, string>
        {
            { "verif-hash", "invalid_hash_value" }
        };

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Flutterwave, payload, headers, secretHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_FlutterwaveMissingHeader_ShouldReturnFalse()
    {
        // Arrange
        const string secretHash = "flw_webhook_secret_hash_9876";
        const string payload = """{"event":"charge.completed","data":{"id":12345}}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Flutterwave, payload, headers, secretHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_PaystackValidHmacSha512_ShouldReturnTrue()
    {
        // Arrange
        const string secretKey = "sk_test_paystack_secret_key_12345";
        const string payload = """{"event":"transfer.success","data":{"reference":"REF-123","amount":5000}}""";

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var validSignature = Convert.ToHexStringLower(hashBytes);

        var headers = new Dictionary<string, string>
        {
            { "x-paystack-signature", validSignature }
        };

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Paystack, payload, headers, secretKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifySignature_PaystackTamperedPayload_ShouldReturnFalse()
    {
        // Arrange
        const string secretKey = "sk_test_paystack_secret_key_12345";
        const string originalPayload = """{"event":"transfer.success","data":{"reference":"REF-123","amount":5000}}""";
        const string tamperedPayload = """{"event":"transfer.success","data":{"reference":"REF-123","amount":9999}}""";

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(originalPayload));
        var validSignature = Convert.ToHexStringLower(hashBytes);

        var headers = new Dictionary<string, string>
        {
            { "x-paystack-signature", validSignature }
        };

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Paystack, tamperedPayload, headers, secretKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifySignature_PaystackMissingSignature_ShouldReturnFalse()
    {
        // Arrange
        const string secretKey = "sk_test_paystack_secret_key_12345";
        const string payload = """{"event":"transfer.success"}""";
        var headers = new Dictionary<string, string>();

        // Act
        var result = _verifier.VerifySignature(PaymentProvider.Paystack, payload, headers, secretKey);

        // Assert
        Assert.False(result);
    }
}
