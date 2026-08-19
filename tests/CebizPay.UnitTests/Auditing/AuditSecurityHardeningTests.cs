using System.Text.Json;
using CebizPay.Application.Common.Security;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class AuditSecurityHardeningTests
{
    private readonly AuditSanitizer _sanitizer = new();

    [Fact]
    public void Sanitize_DeeplyNestedPayloadWithMultipleSensitiveFields_ShouldRedactAllSensitives()
    {
        // Arrange
        var complexPayload = new
        {
            transfer = new
            {
                amount = 50000.00m,
                currency = "NGN",
                auth = new
                {
                    pin = "1234",
                    password = "SuperSecretPassword123!",
                    otp = "654321",
                    token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.sdfsdf",
                    apiKey = "sk_live_1234567890abcdef",
                    mfaSecret = "JBSWY3DPEHPK3PXP"
                },
                paymentDetails = new
                {
                    cardNumber = "4111111111111111",
                    pan = "5105105105105100",
                    cvv = "123",
                    cvc = "456",
                    database_connectionString = "Server=db;Database=pay;User Id=postgres;Password=secret;"
                },
                metadata = new
                {
                    clientVersion = "2.1.0",
                    isMobile = true
                }
            }
        };

        var rawJson = JsonSerializer.Serialize(complexPayload);

        // Act
        var sanitizedJson = _sanitizer.Sanitize(rawJson);

        // Assert
        Assert.NotNull(sanitizedJson);

        // Verify none of the sensitive values exist in sanitizedJson
        Assert.DoesNotContain("1234", sanitizedJson);
        Assert.DoesNotContain("SuperSecretPassword123!", sanitizedJson);
        Assert.DoesNotContain("654321", sanitizedJson);
        Assert.DoesNotContain("sk_live_1234567890abcdef", sanitizedJson);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", sanitizedJson);
        Assert.DoesNotContain("456", sanitizedJson);

        // Verify safe fields remain
        Assert.Contains("50000", sanitizedJson);
        Assert.Contains("NGN", sanitizedJson);
        Assert.Contains("2.1.0", sanitizedJson);
        Assert.Contains("[REDACTED]", sanitizedJson);
    }

    [Fact]
    public void Sanitize_ArrayOfObjectsWithSensitiveFields_ShouldRedactAllArrayElements()
    {
        // Arrange
        var listPayload = new[]
        {
            new { id = 1, passwordHash = "hash-123", email = "user1@example.com" },
            new { id = 2, passwordHash = "hash-456", email = "user2@example.com" }
        };

        var rawJson = JsonSerializer.Serialize(listPayload);

        // Act
        var sanitizedJson = _sanitizer.Sanitize(rawJson);

        // Assert
        Assert.DoesNotContain("hash-123", sanitizedJson);
        Assert.DoesNotContain("hash-456", sanitizedJson);
        Assert.Contains("user1@example.com", sanitizedJson);
        Assert.Contains("user2@example.com", sanitizedJson);
    }

    [Fact]
    public void Sanitize_NullInput_ShouldReturnNull()
    {
        var result = _sanitizer.Sanitize(null);
        Assert.Null(result);
    }

    [Fact]
    public void Sanitize_PlainTextStringWithSensitiveKeys_ShouldRedactUsingRegex()
    {
        var rawText = "Connecting to service with password=SuperSecret and apiKey: 123456";
        var result = _sanitizer.Sanitize(rawText);

        Assert.NotNull(result);
        Assert.DoesNotContain("SuperSecret", result);
        Assert.DoesNotContain("123456", result);
        Assert.Contains("[REDACTED]", result);
    }
}
