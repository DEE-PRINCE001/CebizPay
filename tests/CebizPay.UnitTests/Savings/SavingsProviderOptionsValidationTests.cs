using System.ComponentModel.DataAnnotations;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Savings;

public sealed class SavingsProviderOptionsValidationTests
{
    [Fact]
    public void SavingsOptions_Default_ShouldHaveMockActiveProvider()
    {
        var options = new SavingsOptions();
        Assert.Equal("Mock", options.ActiveProvider);
    }

    [Fact]
    public void CowrywiseOptions_WhenDisabledAndSecretsMissing_ShouldPassValidation()
    {
        var options = new CowrywiseOptions
        {
            Enabled = false,
            ClientId = string.Empty,
            ClientSecret = string.Empty
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void CowrywiseOptions_WhenEnabledAndClientIdMissing_ShouldFailValidation()
    {
        var options = new CowrywiseOptions
        {
            Enabled = true,
            ClientId = string.Empty,
            ClientSecret = "secret_123"
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CowrywiseOptions.ClientId)));
    }

    [Fact]
    public void CowrywiseOptions_WhenEnabledAndClientSecretMissing_ShouldFailValidation()
    {
        var options = new CowrywiseOptions
        {
            Enabled = true,
            ClientId = "client_123",
            ClientSecret = string.Empty
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CowrywiseOptions.ClientSecret)));
    }

    [Fact]
    public void CowrywiseOptions_WhenEnabledAndBaseUrlInvalid_ShouldFailValidation()
    {
        var options = new CowrywiseOptions
        {
            Enabled = true,
            ClientId = "client_123",
            ClientSecret = "secret_123",
            BaseUrl = "not-a-valid-url"
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CowrywiseOptions.BaseUrl)));
    }

    [Fact]
    public void CowrywiseOptions_WhenEnabledAndValid_ShouldPassValidation()
    {
        var options = new CowrywiseOptions
        {
            Enabled = true,
            ClientId = "cw_client_abc",
            ClientSecret = "cw_secret_xyz",
            BaseUrl = "https://sandbox.embed.cowrywise.com/api/v1",
            TimeoutSeconds = 30
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void AnchorOptions_WhenDisabledAndSecretsMissing_ShouldPassValidation()
    {
        var options = new AnchorOptions
        {
            Enabled = false,
            ApiKey = string.Empty,
            ParentFboAccountId = string.Empty
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void AnchorOptions_WhenEnabledAndApiKeyMissing_ShouldFailValidation()
    {
        var options = new AnchorOptions
        {
            Enabled = true,
            ApiKey = string.Empty,
            ParentFboAccountId = "anc_acc_123"
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AnchorOptions.ApiKey)));
    }

    [Fact]
    public void AnchorOptions_WhenEnabledAndParentFboAccountIdMissing_ShouldFailValidation()
    {
        var options = new AnchorOptions
        {
            Enabled = true,
            ApiKey = "key_123",
            ParentFboAccountId = string.Empty
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AnchorOptions.ParentFboAccountId)));
    }

    [Fact]
    public void AnchorOptions_WhenEnabledAndBaseUrlInvalid_ShouldFailValidation()
    {
        var options = new AnchorOptions
        {
            Enabled = true,
            ApiKey = "key_123",
            ParentFboAccountId = "anc_acc_123",
            BaseUrl = "invalid-url"
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AnchorOptions.BaseUrl)));
    }

    [Fact]
    public void AnchorOptions_WhenEnabledAndValid_ShouldPassValidation()
    {
        var options = new AnchorOptions
        {
            Enabled = true,
            ApiKey = "anc_live_key_xyz",
            ParentFboAccountId = "anc_fbo_987",
            BaseUrl = "https://api.getanchor.co/api/v1",
            TimeoutSeconds = 45
        };

        var context = new ValidationContext(options);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(options, context, results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    public void ConfigurationBinding_FromConfiguration_BindsOptionsCorrectly()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Savings:ActiveProvider", "Cowrywise" },
            { "Savings:Cowrywise:Enabled", "true" },
            { "Savings:Cowrywise:BaseUrl", "https://api.cowrywise.com/api/v1" },
            { "Savings:Cowrywise:ClientId", "id_test" },
            { "Savings:Cowrywise:ClientSecret", "secret_test" },
            { "Savings:Anchor:Enabled", "true" },
            { "Savings:Anchor:BaseUrl", "https://api.getanchor.co/api/v1" },
            { "Savings:Anchor:ApiKey", "anchor_key_test" },
            { "Savings:Anchor:ParentFboAccountId", "fbo_test" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddOptions<SavingsOptions>()
            .Bind(configuration.GetSection(SavingsOptions.SectionName));
        services.AddOptions<CowrywiseOptions>()
            .Bind(configuration.GetSection(CowrywiseOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddOptions<AnchorOptions>()
            .Bind(configuration.GetSection(AnchorOptions.SectionName))
            .ValidateDataAnnotations();

        var sp = services.BuildServiceProvider();

        var savings = sp.GetRequiredService<IOptions<SavingsOptions>>().Value;
        var cowrywise = sp.GetRequiredService<IOptions<CowrywiseOptions>>().Value;
        var anchor = sp.GetRequiredService<IOptions<AnchorOptions>>().Value;

        Assert.Equal("Cowrywise", savings.ActiveProvider);
        Assert.True(cowrywise.Enabled);
        Assert.Equal("id_test", cowrywise.ClientId);
        Assert.Equal("secret_test", cowrywise.ClientSecret);
        Assert.True(anchor.Enabled);
        Assert.Equal("anchor_key_test", anchor.ApiKey);
        Assert.Equal("fbo_test", anchor.ParentFboAccountId);
    }
}
