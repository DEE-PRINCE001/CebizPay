using CebizPay.Application.Common.Interfaces.Payments;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentProviderResult"/> factory methods and classification models.
/// </summary>
public sealed class PaymentProviderResultTests
{
    [Fact]
    public void Success_Factory_ShouldInitializeCorrectProperties()
    {
        // Act
        var result = PaymentProviderResult.Success("PSTK_REF_12345", "{\"auth\":\"pin\"}");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Success, result.Status);
        Assert.Equal("PSTK_REF_12345", result.ProviderReference);
        Assert.Null(result.FailureCode);
        Assert.Null(result.FailureReason);
        Assert.Equal("{\"auth\":\"pin\"}", result.SafeMetadata);
    }

    [Fact]
    public void BusinessFailure_Factory_ShouldInitializeCorrectProperties()
    {
        // Act
        var result = PaymentProviderResult.BusinessFailure("ERR_INVALID_ACCOUNT", "Account number does not exist", "{\"bank_code\":\"058\"}");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.BusinessFailure, result.Status);
        Assert.Null(result.ProviderReference);
        Assert.Equal("ERR_INVALID_ACCOUNT", result.FailureCode);
        Assert.Equal("Account number does not exist", result.FailureReason);
        Assert.Equal("{\"bank_code\":\"058\"}", result.SafeMetadata);
    }

    [Fact]
    public void TechnicalFailure_Factory_ShouldInitializeCorrectProperties()
    {
        // Act
        var result = PaymentProviderResult.TechnicalFailure("HTTP_503", "Gateway temporarily unavailable", "{\"retry_after\":30}");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.TechnicalFailure, result.Status);
        Assert.Null(result.ProviderReference);
        Assert.Equal("HTTP_503", result.FailureCode);
        Assert.Equal("Gateway temporarily unavailable", result.FailureReason);
        Assert.Equal("{\"retry_after\":30}", result.SafeMetadata);
    }

    [Fact]
    public void Unknown_Factory_ShouldInitializeCorrectProperties()
    {
        // Act
        var result = PaymentProviderResult.Unknown("Read timeout after 45s", "{\"socket_timeout\":true}");

        // Assert
        Assert.Equal(PaymentProviderResultStatus.Unknown, result.Status);
        Assert.Null(result.ProviderReference);
        Assert.Null(result.FailureCode);
        Assert.Equal("Read timeout after 45s", result.FailureReason);
        Assert.Equal("{\"socket_timeout\":true}", result.SafeMetadata);
    }
}
