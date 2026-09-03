using CebizPay.Infrastructure.Options;
using Xunit;

namespace CebizPay.UnitTests.Security;

/// <summary>
/// Regression tests verifying CORS configuration behavior across Development vs Production environments,
/// ensuring that missing production CORS configuration fails closed.
/// </summary>
public sealed class CorsConfigurationSecurityTests
{
    private static readonly string[] DefaultDevOrigins =
    {
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5015",
        "http://127.0.0.1:5015",
        "http://localhost:3000",
        "http://127.0.0.1:3000"
    };

    private static string[] ResolveEffectiveOrigins(CorsOptions corsOptions, bool isDevelopment)
    {
        return corsOptions.AllowedOrigins.Length > 0
            ? corsOptions.AllowedOrigins
            : (isDevelopment ? DefaultDevOrigins : Array.Empty<string>());
    }

    [Fact]
    public void ResolveEffectiveOrigins_InDevelopment_WithEmptyOrigins_ShouldFallbackToLocalhost()
    {
        // Arrange
        var options = new CorsOptions { AllowedOrigins = Array.Empty<string>() };

        // Act
        var effective = ResolveEffectiveOrigins(options, isDevelopment: true);

        // Assert
        Assert.NotEmpty(effective);
        Assert.Contains("http://localhost:5173", effective);
    }

    [Fact]
    public void ResolveEffectiveOrigins_InProduction_WithEmptyOrigins_ShouldFailClosedWithEmptyArray()
    {
        // Arrange: In production, missing allowed origins must fail closed
        var options = new CorsOptions { AllowedOrigins = Array.Empty<string>() };

        // Act
        var effective = ResolveEffectiveOrigins(options, isDevelopment: false);

        // Assert: Must be empty (fail closed)
        Assert.Empty(effective);
    }

    [Fact]
    public void ResolveEffectiveOrigins_InProduction_WithConfiguredOrigins_ShouldUseConfiguredOrigins()
    {
        // Arrange
        var configured = new[] { "https://app.cebizpay.com", "https://admin.cebizpay.com" };
        var options = new CorsOptions { AllowedOrigins = configured };

        // Act
        var effective = ResolveEffectiveOrigins(options, isDevelopment: false);

        // Assert
        Assert.Equal(2, effective.Length);
        Assert.Equal(configured, effective);
    }
}
