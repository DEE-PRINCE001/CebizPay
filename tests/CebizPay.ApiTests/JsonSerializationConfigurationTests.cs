using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Savings.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class JsonSerializationConfigurationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JsonSerializationConfigurationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Program_RegistersJsonStringEnumConverter_InMvcJsonOptions()
    {
        using var scope = _factory.Services.CreateScope();
        var mvcOptions = scope.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value;

        var hasStringEnumConverter = mvcOptions.JsonSerializerOptions.Converters
            .Any(c => c is JsonStringEnumConverter);

        Assert.True(hasStringEnumConverter, "JsonStringEnumConverter should be registered in MVC JsonOptions.");
    }

    [Fact]
    public void Program_RegistersJsonStringEnumConverter_InHttpJsonOptions()
    {
        using var scope = _factory.Services.CreateScope();
        var httpOptions = scope.ServiceProvider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value;

        var hasStringEnumConverter = httpOptions.SerializerOptions.Converters
            .Any(c => c is JsonStringEnumConverter);

        Assert.True(hasStringEnumConverter, "JsonStringEnumConverter should be registered in HttpJsonOptions.");
    }

    [Fact]
    public void MvcJsonSerializerOptions_DeserializesEnumsFromStringsAndNumbers()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        // 1. Verify deserialization from string names
        var fromStringJson = """{"employmentType":"Remote","contributionFrequency":"Daily","repaymentFrequency":"Monthly"}""";
        var parsedFromString = JsonSerializer.Deserialize<TestEnumContainer>(fromStringJson, options);

        Assert.NotNull(parsedFromString);
        Assert.Equal(EmploymentType.Remote, parsedFromString.EmploymentType);
        Assert.Equal(SavingsContributionFrequency.Daily, parsedFromString.ContributionFrequency);
        Assert.Equal(RepaymentFrequency.Monthly, parsedFromString.RepaymentFrequency);

        // 2. Verify deserialization from integer numbers (backwards compatibility)
        var fromNumberJson = """{"employmentType":4,"contributionFrequency":1,"repaymentFrequency":1}""";
        var parsedFromNumber = JsonSerializer.Deserialize<TestEnumContainer>(fromNumberJson, options);

        Assert.NotNull(parsedFromNumber);
        Assert.Equal(EmploymentType.Remote, parsedFromNumber.EmploymentType);
        Assert.Equal(SavingsContributionFrequency.Daily, parsedFromNumber.ContributionFrequency);
        Assert.Equal(RepaymentFrequency.Monthly, parsedFromNumber.RepaymentFrequency);

        // 3. Verify serialization produces string representation
        var container = new TestEnumContainer
        {
            EmploymentType = EmploymentType.Remote,
            ContributionFrequency = SavingsContributionFrequency.Daily,
            RepaymentFrequency = RepaymentFrequency.Monthly
        };

        var serializedJson = JsonSerializer.Serialize(container, options);
        Assert.Contains("\"employmentType\":\"Remote\"", serializedJson);
        Assert.Contains("\"contributionFrequency\":\"Daily\"", serializedJson);
        Assert.Contains("\"repaymentFrequency\":\"Monthly\"", serializedJson);
    }

    private sealed class TestEnumContainer
    {
        public EmploymentType EmploymentType { get; set; }
        public SavingsContributionFrequency ContributionFrequency { get; set; }
        public RepaymentFrequency RepaymentFrequency { get; set; }
    }
}
