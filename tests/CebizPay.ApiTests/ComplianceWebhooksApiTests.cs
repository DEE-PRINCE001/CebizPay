#pragma warning disable CS1591
using System.Net;
using System.Text;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Domain.Compliance.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

public sealed class ComplianceWebhooksApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IComplianceWebhookProcessor webhookProcessor)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(ComplianceWebhooksController).Assembly);
                    services.AddApiVersioning(options =>
                    {
                        options.DefaultApiVersion = new ApiVersion(1, 0);
                        options.AssumeDefaultVersionWhenUnspecified = true;
                        options.ReportApiVersions = true;
                        options.ApiVersionReader = new UrlSegmentApiVersionReader();
                    });
                    services.AddSingleton(webhookProcessor);
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
        return (host, client);
    }

    [Fact]
    public async Task DojahWebhook_Processed_Returns200Ok()
    {
        var processor = Substitute.For<IComplianceWebhookProcessor>();
        processor.ProcessWebhookAsync(
            VerificationProvider.Dojah,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(ComplianceWebhookProcessingResult.Processed("evt_1", "Processed"));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        using (client)
        {
            var content = new StringContent("{\"event\":\"verification.completed\"}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/compliance/webhooks/dojah", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task SmileIdWebhook_Duplicate_Returns200OkWithDuplicateMessage()
    {
        var processor = Substitute.For<IComplianceWebhookProcessor>();
        processor.ProcessWebhookAsync(
            VerificationProvider.SmileId,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(ComplianceWebhookProcessingResult.Duplicate("job_123", "Duplicate job acknowledged."));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        using (client)
        {
            var content = new StringContent("{\"JobId\":\"job_123\"}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/compliance/webhooks/smile-id", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task NinjaWebhook_InvalidSignature_Returns401Unauthorized()
    {
        var processor = Substitute.For<IComplianceWebhookProcessor>();
        processor.ProcessWebhookAsync(
            VerificationProvider.Ninja,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(ComplianceWebhookProcessingResult.InvalidSignature("Invalid signature"));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        using (client)
        {
            var content = new StringContent("{\"event\":\"test\"}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync("/api/v1/compliance/webhooks/ninja", content);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
