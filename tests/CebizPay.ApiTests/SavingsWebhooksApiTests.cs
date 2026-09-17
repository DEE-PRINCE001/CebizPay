#pragma warning disable CA1848, CS1591
using System.Net;
using System.Text;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Savings;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Savings.Entities;
using CebizPay.Domain.Savings.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class SavingsWebhooksApiTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task<(IHost host, HttpClient client, ApplicationDbContext dbContext)> CreateTestServer(
        ISavingsProviderFactory providerFactory)
    {
        var dbContext = CreateInMemoryDbContext();

        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(SavingsWebhooksController).Assembly);
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(providerFactory);
                    services.AddSingleton<IApplicationDbContext>(dbContext);
                    services.AddLogging();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                });
            })
            .StartAsync();

        var client = host.GetTestClient();
        return (host, client, dbContext);
    }

    [Fact]
    public async Task CowrywiseWebhook_EmptyBody_Returns400BadRequest()
    {
        // Arrange
        var factory = Substitute.For<ISavingsProviderFactory>();
        var (host, client, dbContext) = await CreateTestServer(factory);

        using (host)
        await using (dbContext)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/savings/webhooks/cowrywise")
            {
                Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task CowrywiseWebhook_InvalidSignature_Returns401Unauthorized()
    {
        // Arrange
        var provider = Substitute.For<ISavingsProvider>();
        provider.ProviderName.Returns("Cowrywise");
        provider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns((SavingsWebhookEvent?)null);

        var factory = Substitute.For<ISavingsProviderFactory>();
        factory.GetProvider("Cowrywise").Returns(provider);

        var (host, client, dbContext) = await CreateTestServer(factory);

        using (host)
        await using (dbContext)
        {
            var payload = """{"event":"interest_accrued","data":{"id":"cw-evt-1"}}""";
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/savings/webhooks/cowrywise")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-cowrywise-signature", "invalid-sig");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task CowrywiseWebhook_ValidSignature_SyncsAccountYieldAndReturns200Ok()
    {
        // Arrange
        const string extPlanId = "cw-plan-webhook-1";
        var provider = Substitute.For<ISavingsProvider>();
        provider.ProviderName.Returns("Cowrywise");

        provider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new SavingsWebhookEvent(
                "interest_accrued",
                "Cowrywise",
                extPlanId,
                null,
                500m,
                DateTime.UtcNow,
                "cw-raw-evt-1",
                "{\"event\":\"interest_accrued\"}"));

        provider.GetPositionAsync(extPlanId, Arg.Any<CancellationToken>())
            .Returns(new ExternalSavingsPosition(
                extPlanId,
                100_000m,
                500m,
                500m,
                false,
                "active",
                DateTime.UtcNow));

        var factory = Substitute.For<ISavingsProviderFactory>();
        factory.GetProvider("Cowrywise").Returns(provider);

        var (host, client, dbContext) = await CreateTestServer(factory);

        using (host)
        await using (dbContext)
        {
            var plan = SavingsPlan.CreateFixedLockPlan(
                null, "user-hook-1", SavingsOwnerType.Individual, "Cowrywise Hook Plan", null,
                Currency.NGN, 0.12m, 10_000m, 1_000_000m, 30, 90, 1);
            dbContext.SavingsPlans.Add(plan);

            var account = SavingsAccount.CreateFixedLockAccount(
                plan.Id, "user-hook-1", null, Currency.NGN, 0.12m, 1, 90, DateTime.UtcNow.AddDays(-15));
            account.LinkExternalProvider("Cowrywise", "cw-cust-hook-1", extPlanId, "active");
            account.RecordContribution(100_000m, Guid.NewGuid(), "REF-HOOK-001");
            dbContext.SavingsAccounts.Add(account);
            await dbContext.SaveChangesAsync();

            var payload = """{"event":"interest_accrued","data":{"plan_id":"cw-plan-webhook-1"}}""";
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/savings/webhooks/cowrywise")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-cowrywise-signature", "valid-signature");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var updatedAccount = await dbContext.SavingsAccounts
                .Include(a => a.InterestAccruals)
                .FirstOrDefaultAsync(a => a.Id == account.Id);

            Assert.NotNull(updatedAccount);
            Assert.Equal(500m, updatedAccount.AccruedInterest);
            Assert.Single(updatedAccount.InterestAccruals);
            Assert.Equal(500m, updatedAccount.InterestAccruals.First().Amount);
        }
    }

    [Fact]
    public async Task AnchorWebhook_ValidSignature_Returns200Ok()
    {
        // Arrange
        const string extPlanId = "anc-subacc-webhook-1";
        var provider = Substitute.For<ISavingsProvider>();
        provider.ProviderName.Returns("Anchor");

        provider.ParseWebhookAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new SavingsWebhookEvent(
                "subaccount.credited",
                "Anchor",
                extPlanId,
                null,
                null,
                DateTime.UtcNow,
                "anc-evt-123",
                "{\"data\":{}}"));

        var factory = Substitute.For<ISavingsProviderFactory>();
        factory.GetProvider("Anchor").Returns(provider);

        var (host, client, dbContext) = await CreateTestServer(factory);

        using (host)
        await using (dbContext)
        {
            var payload = """{"data":{"type":"events","id":"anc-evt-123"}}""";
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/savings/webhooks/anchor")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("x-anchor-signature", "valid-anchor-sig");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task GenericProviderWebhook_UnknownProvider_Returns404NotFound()
    {
        // Arrange
        var factory = Substitute.For<ISavingsProviderFactory>();
        factory.GetProvider("UnknownProvider").Returns(_ => throw new NotSupportedException("Unknown provider"));

        var (host, client, dbContext) = await CreateTestServer(factory);

        using (host)
        await using (dbContext)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/savings/webhooks/UnknownProvider")
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
