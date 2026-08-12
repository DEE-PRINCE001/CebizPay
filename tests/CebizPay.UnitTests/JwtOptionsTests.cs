using System.ComponentModel.DataAnnotations;
using CebizPay.Infrastructure.Options;
using Xunit;

namespace CebizPay.UnitTests;

public sealed class JwtOptionsTests
{
    [Fact]
    public void JwtOptions_WithShortSecret_ShouldFailValidation()
    {
        // Arrange
        var options = new JwtOptions
        {
            Secret = "short_key",
            Issuer = "CebizPay",
            Audience = "CebizPay.Clients",
            ExpirationInMinutes = 60
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(JwtOptions.Secret)));
    }

    [Fact]
    public void JwtOptions_WithValid256BitSecret_ShouldPassValidation()
    {
        // Arrange
        var options = new JwtOptions
        {
            Secret = "a_very_secure_and_sufficiently_long_secret_key_256bit!",
            Issuer = "CebizPay",
            Audience = "CebizPay.Clients",
            ExpirationInMinutes = 60
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }
}
