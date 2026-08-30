using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.UseCases.Wallet.ExternalAccounts;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

/// <summary>
/// API integration tests for external funding account endpoints on <see cref="WalletController"/>.
/// </summary>
public sealed class WalletExternalFundingAccountApiTests
{
    private static IHostBuilder CreateHostBuilder(ISender mediator)
    {
        return new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(WalletController).Assembly);
                    services.AddAuthentication("TestScheme")
                            .AddScheme<AuthenticationSchemeOptions, TestExternalAccountAuthHandler>("TestScheme", _ => { });
                    services.AddAuthorization();
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(mediator);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });
    }

    [Fact]
    public async Task GetExternalAccounts_ShouldReturnOkWithAccountList()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var accountDto = new ExternalFundingAccountResponseDto(
            Id: Guid.NewGuid(),
            WalletId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderCustomerReference: "MNFY_CUST_1",
            ProviderAccountReference: "MNFY_ACC_1",
            AccountNumber: "0123456789",
            AccountName: "Test Account",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: "NGN",
            Status: "Active",
            IsPrimary: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        mediator.Send(Arg.Any<GetExternalFundingAccountsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExternalFundingAccountResponseDto> { accountDto });

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/v1/wallet/external-accounts");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<ExternalFundingAccountResponseDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Single(body);
        Assert.Equal("0123456789", body[0].AccountNumber);
    }

    [Fact]
    public async Task SetPrimaryAccount_ShouldReturnOkWithUpdatedAccount()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var accountId = Guid.NewGuid();
        var accountDto = new ExternalFundingAccountResponseDto(
            Id: accountId,
            WalletId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderCustomerReference: null,
            ProviderAccountReference: null,
            AccountNumber: "0123456789",
            AccountName: "Test Account",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: "NGN",
            Status: "Active",
            IsPrimary: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow);

        mediator.Send(Arg.Any<SetPrimaryExternalFundingAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(accountDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync($"/api/v1/wallet/external-accounts/{accountId}/primary", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExternalFundingAccountResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.True(body.IsPrimary);
    }

    [Fact]
    public async Task ProvisionMonnifyAccount_ShouldReturnOkWithProvisionedAccount()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var accountId = Guid.NewGuid();
        var accountDto = new ExternalFundingAccountResponseDto(
            Id: accountId,
            WalletId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderCustomerReference: "usr-1",
            ProviderAccountReference: "MNFY_123",
            AccountNumber: "8899001122",
            AccountName: "Monnify Test Account",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: "NGN",
            Status: "Active",
            IsPrimary: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        mediator.Send(Arg.Any<ProvisionMonnifyExternalFundingAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(accountDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.PostAsync("/api/v1/wallet/external-accounts/monnify", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExternalFundingAccountResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal("8899001122", body.AccountNumber);
        Assert.Equal("Monnify", body.Provider);
    }

    [Fact]
    public async Task GetExternalAccountById_WhenExists_ShouldReturnOk()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var accountId = Guid.NewGuid();
        var accountDto = new ExternalFundingAccountResponseDto(
            Id: accountId,
            WalletId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderCustomerReference: null,
            ProviderAccountReference: null,
            AccountNumber: "1234567890",
            AccountName: "Get Acct",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: "NGN",
            Status: "Active",
            IsPrimary: true,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: null);

        mediator.Send(Arg.Any<GetExternalFundingAccountByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(accountDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/api/v1/wallet/external-accounts/{accountId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExternalFundingAccountResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(accountId, body.Id);
    }

    [Fact]
    public async Task DeactivateAccount_ShouldReturnOkWithSuspendedStatus()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var accountId = Guid.NewGuid();
        var accountDto = new ExternalFundingAccountResponseDto(
            Id: accountId,
            WalletId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderCustomerReference: null,
            ProviderAccountReference: null,
            AccountNumber: "1234567890",
            AccountName: "Deact Acct",
            BankCode: "035",
            BankName: "Wema Bank",
            Currency: "NGN",
            Status: "Suspended",
            IsPrimary: false,
            CreatedAtUtc: DateTime.UtcNow,
            UpdatedAtUtc: DateTime.UtcNow);

        mediator.Send(Arg.Any<DeactivateExternalFundingAccountCommand>(), Arg.Any<CancellationToken>())
            .Returns(accountDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.DeleteAsync($"/api/v1/wallet/external-accounts/{accountId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ExternalFundingAccountResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal("Suspended", body.Status);
    }

    [Fact]
    public async Task GetFundingTransaction_WhenExists_ShouldReturnOk()
    {
        // Arrange
        var mediator = Substitute.For<ISender>();
        var fundingId = Guid.NewGuid();
        var fundingDto = new FundingTransactionResponseDto(
            Id: fundingId,
            WalletId: Guid.NewGuid(),
            ExternalFundingAccountId: Guid.NewGuid(),
            Provider: "Monnify",
            ProviderTransactionReference: "MNFY_TX_123",
            FundingChannel: "VirtualAccount",
            Amount: 50000m,
            FeeAmount: 100m,
            NetCreditedAmount: 49900m,
            Currency: "NGN",
            Status: "Completed",
            LedgerTransactionId: Guid.NewGuid(),
            CreatedAtUtc: DateTime.UtcNow,
            CompletedAtUtc: DateTime.UtcNow,
            FailureReason: null);

        mediator.Send(Arg.Any<GetFundingTransactionByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(fundingDto);

        using var host = await CreateHostBuilder(mediator).StartAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/api/v1/wallet/funding/{fundingId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FundingTransactionResponseDto>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(body);
        Assert.Equal(fundingId, body.Id);
        Assert.Equal(50000m, body.Amount);
    }

    private sealed class TestExternalAccountAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestExternalAccountAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
                new Claim(ClaimTypes.Email, "testuser@cebizpay.com")
            };
            var identity = new ClaimsIdentity(claims, "TestScheme");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "TestScheme");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
