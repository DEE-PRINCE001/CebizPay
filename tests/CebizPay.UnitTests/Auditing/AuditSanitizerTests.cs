using System.Text.Json;
using CebizPay.Application.Common.Security;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class AuditSanitizerTests
{
    private readonly AuditSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_WithNullPayload_ShouldReturnNull()
    {
        Assert.Null(_sanitizer.Sanitize(null));
        Assert.Null(_sanitizer.SanitizeJsonString(null));
    }

    [Theory]
    [InlineData("password", "SuperSecret123!")]
    [InlineData("passwordHash", "AQAAAAIAAYagAAAAEG1...")]
    [InlineData("currentPassword", "OldPass123!")]
    [InlineData("newPassword", "NewPass123!")]
    [InlineData("pin", "1234")]
    [InlineData("transactionPin", "4321")]
    [InlineData("pinHash", "$2a$11$e8...")]
    [InlineData("otp", "584920")]
    [InlineData("otpCode", "123456")]
    [InlineData("mfaSecret", "JBSWY3DPEHPK3PXP")]
    [InlineData("mfaCode", "892103")]
    [InlineData("authenticatorSecret", "HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ")]
    [InlineData("jwt", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...")]
    [InlineData("token", "d8f7e2a1...")]
    [InlineData("accessToken", "eyJhbGciOiJIUz...")]
    [InlineData("refreshToken", "refresh_9123...")]
    [InlineData("apiKey", "sk_live_51H...")]
    [InlineData("apiSecret", "sec_live_99...")]
    [InlineData("pan", "4111111111111111")]
    [InlineData("cardNumber", "5500000000000004")]
    [InlineData("cvv", "123")]
    [InlineData("cvc", "999")]
    [InlineData("connectionString", "Host=db;Password=secret")]
    public void Sanitize_WithSensitiveFields_ShouldRedactAllSensitiveValues(string sensitiveKey, string sensitiveValue)
    {
        // Arrange
        var payload = new Dictionary<string, object>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["name"] = "Alice Johnson",
            ["amount"] = 5000m,
            ["currency"] = "NGN",
            [sensitiveKey] = sensitiveValue
        };

        // Act
        var resultJson = _sanitizer.Sanitize(payload);

        // Assert
        Assert.NotNull(resultJson);
        Assert.DoesNotContain(sensitiveValue, resultJson);

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        Assert.Equal("Alice Johnson", root.GetProperty("name").GetString());
        Assert.Equal(5000m, root.GetProperty("amount").GetDecimal());
        Assert.Equal("NGN", root.GetProperty("currency").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty(sensitiveKey).GetString());
    }

    [Fact]
    public void Sanitize_WithNestedObject_ShouldRedactDeepSensitiveFields()
    {
        // Arrange
        var payload = new
        {
            TransactionReference = "CBZPT-12345",
            Amount = 1000m,
            User = new
            {
                Email = "user@example.com",
                Security = new
                {
                    TransactionPin = "9988",
                    PasswordHash = "hash123",
                    MfaSecret = "secretXYZ"
                }
            },
            Cards = new[]
            {
                new { CardHolder = "John", CardNumber = "4111222233334444", Cvv = "987" }
            }
        };

        // Act
        var resultJson = _sanitizer.Sanitize(payload);

        // Assert
        Assert.NotNull(resultJson);
        Assert.DoesNotContain("9988", resultJson);
        Assert.DoesNotContain("hash123", resultJson);
        Assert.DoesNotContain("secretXYZ", resultJson);
        Assert.DoesNotContain("4111222233334444", resultJson);
        Assert.DoesNotContain("987", resultJson);

        using var doc = JsonDocument.Parse(resultJson);
        var root = doc.RootElement;
        Assert.Equal("CBZPT-12345", root.GetProperty("transactionReference").GetString());
        Assert.Equal("user@example.com", root.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("user").GetProperty("security").GetProperty("transactionPin").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("user").GetProperty("security").GetProperty("passwordHash").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("user").GetProperty("security").GetProperty("mfaSecret").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("cards")[0].GetProperty("cardNumber").GetString());
        Assert.Equal("[REDACTED]", root.GetProperty("cards")[0].GetProperty("cvv").GetString());
    }

    [Fact]
    public void SanitizeJsonString_WithRawJson_ShouldRedactSensitiveKeys()
    {
        // Arrange
        var rawJson = "{\"username\":\"admin\",\"password\":\"P@ssw0rd123\",\"pin\":\"1234\"}";

        // Act
        var result = _sanitizer.SanitizeJsonString(rawJson);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain("P@ssw0rd123", result);
        Assert.DoesNotContain("1234", result);
        Assert.Contains("[REDACTED]", result);
    }
}
