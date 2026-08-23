using System.ComponentModel.DataAnnotations;
using System.Net;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit and startup validation tests for FlutterwaveOptions, PaystackOptions, and VtuGateOptions.
/// </summary>
public sealed class ProviderOptionsValidationTests
{
    // =========================================================================
    // FlutterwaveOptions Tests
    // =========================================================================

    [Fact]
    public void FlutterwaveOptions_WhenDisabledAndSecretsMissing_ShouldPassValidation()
    {
        // Arrange
        var options = new FlutterwaveOptions
        {
            Enabled = false,
            SecretKey = string.Empty,
            BaseUrl = "https://api.flutterwave.com",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void FlutterwaveOptions_WhenEnabledAndSecretKeyMissing_ShouldFailValidation()
    {
        // Arrange
        var options = new FlutterwaveOptions
        {
            Enabled = true,
            SecretKey = string.Empty,
            BaseUrl = "https://api.flutterwave.com",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(FlutterwaveOptions.SecretKey)));
    }

    [Fact]
    public void FlutterwaveOptions_WhenEnabledAndValid_ShouldPassValidation()
    {
        // Arrange
        var options = new FlutterwaveOptions
        {
            Enabled = true,
            SecretKey = "FLWSECK_TEST-valid-mock-key-12345",
            BaseUrl = "https://api.flutterwave.com",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("")]
    public void FlutterwaveOptions_WhenEnabledAndInvalidBaseUrl_ShouldFailValidation(string invalidUrl)
    {
        // Arrange
        var options = new FlutterwaveOptions
        {
            Enabled = true,
            SecretKey = "FLWSECK_TEST-valid-mock-key-12345",
            BaseUrl = invalidUrl,
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(FlutterwaveOptions.BaseUrl)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(301)]
    public void FlutterwaveOptions_WhenEnabledAndInvalidTimeout_ShouldFailValidation(int invalidTimeout)
    {
        // Arrange
        var options = new FlutterwaveOptions
        {
            Enabled = true,
            SecretKey = "FLWSECK_TEST-valid-mock-key-12345",
            BaseUrl = "https://api.flutterwave.com",
            TimeoutSeconds = invalidTimeout
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(FlutterwaveOptions.TimeoutSeconds)));
    }

    // =========================================================================
    // PaystackOptions Tests
    // =========================================================================

    [Fact]
    public void PaystackOptions_WhenDisabledAndSecretsMissing_ShouldPassValidation()
    {
        // Arrange
        var options = new PaystackOptions
        {
            Enabled = false,
            SecretKey = string.Empty,
            BaseUrl = "https://api.paystack.co",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void PaystackOptions_WhenEnabledAndSecretKeyMissing_ShouldFailValidation()
    {
        // Arrange
        var options = new PaystackOptions
        {
            Enabled = true,
            SecretKey = string.Empty,
            BaseUrl = "https://api.paystack.co",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PaystackOptions.SecretKey)));
    }

    [Fact]
    public void PaystackOptions_WhenEnabledAndValid_ShouldPassValidation()
    {
        // Arrange
        var options = new PaystackOptions
        {
            Enabled = true,
            SecretKey = "sk_test_valid_mock_key_12345",
            BaseUrl = "https://api.paystack.co",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("")]
    public void PaystackOptions_WhenEnabledAndInvalidBaseUrl_ShouldFailValidation(string invalidUrl)
    {
        // Arrange
        var options = new PaystackOptions
        {
            Enabled = true,
            SecretKey = "sk_test_valid_mock_key_12345",
            BaseUrl = invalidUrl,
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PaystackOptions.BaseUrl)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(301)]
    public void PaystackOptions_WhenEnabledAndInvalidTimeout_ShouldFailValidation(int invalidTimeout)
    {
        // Arrange
        var options = new PaystackOptions
        {
            Enabled = true,
            SecretKey = "sk_test_valid_mock_key_12345",
            BaseUrl = "https://api.paystack.co",
            TimeoutSeconds = invalidTimeout
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(PaystackOptions.TimeoutSeconds)));
    }

    // =========================================================================
    // VtuGateOptions Tests
    // =========================================================================

    [Fact]
    public void VtuGateOptions_WhenDisabledAndSecretsMissing_ShouldPassValidation()
    {
        // Arrange
        var options = new VtuGateOptions
        {
            Enabled = false,
            ApiKey = string.Empty,
            BaseUrl = "https://vtugate.com/api",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void VtuGateOptions_WhenEnabledAndApiKeyMissing_ShouldFailValidation()
    {
        // Arrange
        var options = new VtuGateOptions
        {
            Enabled = true,
            ApiKey = string.Empty,
            BaseUrl = "https://vtugate.com/api",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VtuGateOptions.ApiKey)));
    }

    [Fact]
    public void VtuGateOptions_WhenEnabledAndValid_ShouldPassValidation()
    {
        // Arrange
        var options = new VtuGateOptions
        {
            Enabled = true,
            ApiKey = "vtugate_mock_api_key_12345",
            BaseUrl = "https://vtugate.com/api",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData("invalid-uri")]
    [InlineData("")]
    public void VtuGateOptions_WhenEnabledAndInvalidBaseUrl_ShouldFailValidation(string invalidUrl)
    {
        // Arrange
        var options = new VtuGateOptions
        {
            Enabled = true,
            ApiKey = "vtugate_mock_api_key_12345",
            BaseUrl = invalidUrl,
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VtuGateOptions.BaseUrl)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(350)]
    public void VtuGateOptions_WhenEnabledAndInvalidTimeout_ShouldFailValidation(int invalidTimeout)
    {
        // Arrange
        var options = new VtuGateOptions
        {
            Enabled = true,
            ApiKey = "vtugate_mock_api_key_12345",
            BaseUrl = "https://vtugate.com/api",
            TimeoutSeconds = invalidTimeout
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();

        // Act
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(VtuGateOptions.TimeoutSeconds)));
    }

    // =========================================================================
    // Startup Dependency Injection ValidateOnStart Tests
    // =========================================================================

    [Fact]
    public void DependencyInjection_WhenProviderEnabledAndMissingSecret_ThrowsOptionsValidationException()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Payments:Flutterwave:Enabled"] = "true",
            ["Payments:Flutterwave:BaseUrl"] = "https://api.flutterwave.com",
            ["Payments:Flutterwave:SecretKey"] = "" // Missing secret
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FlutterwaveOptions>()
            .Bind(config.GetSection(FlutterwaveOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => sp.GetRequiredService<IOptions<FlutterwaveOptions>>().Value);
    }

    [Fact]
    public void DependencyInjection_WhenProviderDisabledAndMissingSecret_ResolvesSuccessfully()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Payments:Flutterwave:Enabled"] = "false",
            ["Payments:Flutterwave:BaseUrl"] = "https://api.flutterwave.com",
            ["Payments:Flutterwave:SecretKey"] = "" // Missing secret is OK when disabled
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<FlutterwaveOptions>()
            .Bind(config.GetSection(FlutterwaveOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<FlutterwaveOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.False(options.Enabled);
    }

    [Fact]
    public void DependencyInjection_WhenProviderEnabledAndValidSecret_ResolvesSuccessfully()
    {
        // Arrange
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["Payments:Paystack:Enabled"] = "true",
            ["Payments:Paystack:BaseUrl"] = "https://api.paystack.co",
            ["Payments:Paystack:SecretKey"] = "sk_test_valid_secret_key_12345",
            ["Payments:Paystack:TimeoutSeconds"] = "30"
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemoryConfig)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<PaystackOptions>()
            .Bind(config.GetSection(PaystackOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptions<PaystackOptions>>().Value;

        // Assert
        Assert.NotNull(options);
        Assert.True(options.Enabled);
        Assert.Equal("sk_test_valid_secret_key_12345", options.SecretKey);
    }

    // =========================================================================
    // Secret Leakage and Log Redaction Verification Tests
    // =========================================================================

    [Fact]
    public async Task FlutterwaveClient_LogsDoNotContainSecretKey()
    {
        // Arrange
        const string mockSecret = "TEST-SECRET-DO-NOT-USE-FLW-12345";
        var options = Options.Create(new FlutterwaveOptions
        {
            Enabled = true,
            BaseUrl = "https://api.flutterwave.com",
            SecretKey = mockSecret,
            TimeoutSeconds = 5
        });

        var testLogger = new TestLogger<FlutterwaveClient>();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": "success",
            "message": "Account resolved",
            "data": {
                "account_number": "0123456789",
                "account_name": "Test Account"
            }
        }
        """);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.flutterwave.com/")
        };

        var client = new FlutterwaveClient(httpClient, options, testLogger);

        // Act
        var result = await client.ResolveAccountAsync("044", "0123456789");

        // Assert
        Assert.True(result.Succeeded);
        foreach (var logMessage in testLogger.CapturedMessages)
        {
            Assert.DoesNotContain(mockSecret, logMessage);
            Assert.DoesNotContain("Bearer", logMessage);
        }
    }

    [Fact]
    public async Task PaystackClient_LogsDoNotContainSecretKey()
    {
        // Arrange
        const string mockSecret = "TEST-SECRET-DO-NOT-USE-PSTK-67890";
        var options = Options.Create(new PaystackOptions
        {
            Enabled = true,
            BaseUrl = "https://api.paystack.co",
            SecretKey = mockSecret,
            TimeoutSeconds = 5
        });

        var testLogger = new TestLogger<PaystackClient>();
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": true,
            "message": "Account resolved",
            "data": {
                "account_number": "0123456789",
                "account_name": "Test Account",
                "bank_id": 9
            }
        }
        """);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.paystack.co/")
        };

        var client = new PaystackClient(httpClient, options, testLogger);

        // Act
        var result = await client.ResolveAccountAsync("058", "0123456789");

        // Assert
        Assert.True(result.Succeeded);
        foreach (var logMessage in testLogger.CapturedMessages)
        {
            Assert.DoesNotContain(mockSecret, logMessage);
            Assert.DoesNotContain("Bearer", logMessage);
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<string> CapturedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            CapturedMessages.Add(formatter(state, exception));
        }
    }
}
