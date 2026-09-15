using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.UseCases.Organizations.Staff;
using CebizPay.Application.UseCases.Organizations.Wallet;
using CebizPay.Application.UseCases.Wallet.Transfer;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Paystack;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.FrontendNeeds;

public sealed class FrontendEndpointsTests
{
    [Fact]
    public async Task BankDirectoryService_WhenCacheHit_ShouldReturnCachedBanksWithoutCallingProvider()
    {
        // Arrange
        var mockCache = Substitute.For<ICacheService>();
        var cachedBanks = new List<BankDto>
        {
            new("Access Bank", "044", "access-bank", null, null, true),
            new("Guaranty Trust Bank", "058", "gtb", null, null, true)
        };

        mockCache.GetAsync<List<BankDto>>("directory:banks:ng", Arg.Any<CancellationToken>())
            .Returns(cachedBanks);

        var httpClient = new HttpClient();
        var options = Options.Create(new PaystackOptions { Enabled = false });
        var paystackClient = new PaystackClient(httpClient, options, NullLogger<PaystackClient>.Instance);

        var service = new BankDirectoryService(paystackClient, mockCache, NullLogger<BankDirectoryService>.Instance);

        // Act
        var result = await service.GetBanksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("Access Bank", result[0].Name);
    }

    [Fact]
    public async Task BankDirectoryService_WhenProviderFails_ShouldReturnFallbackBanks()
    {
        // Arrange
        var mockCache = Substitute.For<ICacheService>();
        mockCache.GetAsync<List<BankDto>>("directory:banks:ng", Arg.Any<CancellationToken>())
            .Returns((List<BankDto>?)null);

        var httpClient = new HttpClient();
        var options = Options.Create(new PaystackOptions { Enabled = true, SecretKey = "invalid_key", BaseUrl = "http://localhost:9999" });
        var paystackClient = new PaystackClient(httpClient, options, NullLogger<PaystackClient>.Instance);

        var service = new BankDirectoryService(paystackClient, mockCache, NullLogger<BankDirectoryService>.Instance);

        // Act
        var result = await service.GetBanksAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains(result, b => b.Name == "Zenith Bank" && b.Code == "057");
        Assert.Contains(result, b => b.Name == "Wema Bank" && b.Code == "035");
    }

    [Fact]
    public void ResolveWalletQueryValidator_EmptyIdentifier_ShouldFailValidation()
    {
        // Arrange
        var validator = new ResolveWalletQueryValidator();
        var query = new ResolveWalletQuery(string.Empty);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Identifier");
    }

    [Fact]
    public void GetOrgWalletOverviewQueryValidator_EmptyOrgId_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetOrgWalletOverviewQueryValidator();
        var query = new GetOrgWalletOverviewQuery(Guid.Empty);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrganizationId");
    }

    [Fact]
    public void GetStaffSalariesQueryValidator_EmptyParameters_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetStaffSalariesQueryValidator();
        var query = new GetStaffSalariesQuery(Guid.Empty, Guid.Empty, -1, 200);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrganizationId");
        Assert.Contains(result.Errors, e => e.PropertyName == "MembershipId");
        Assert.Contains(result.Errors, e => e.PropertyName == "PageNumber");
        Assert.Contains(result.Errors, e => e.PropertyName == "PageSize");
    }

    [Fact]
    public void GetStaffSavingsQueryValidator_EmptyParameters_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetStaffSavingsQueryValidator();
        var query = new GetStaffSavingsQuery(Guid.Empty, Guid.Empty);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrganizationId");
        Assert.Contains(result.Errors, e => e.PropertyName == "MembershipId");
    }

    [Fact]
    public void GetOrgAdminsQueryValidator_EmptyOrgId_ShouldFailValidation()
    {
        // Arrange
        var validator = new GetOrgAdminsQueryValidator();
        var query = new GetOrgAdminsQuery(Guid.Empty);

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "OrganizationId");
    }
}
