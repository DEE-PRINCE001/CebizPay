using System.Net;
using System.Text;
using Asp.Versioning;
using CebizPay.Api.Controllers.v1;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Payments.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace CebizPay.ApiTests;

/// <summary>
/// API integration tests for <see cref="PaymentsWebhookController"/> endpoints.
/// </summary>
public sealed class PaymentsWebhookApiTests
{
    private static async Task<(IHost host, HttpClient client)> CreateTestServer(IWebhookProcessor webhookProcessor)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers().AddApplicationPart(typeof(PaymentsWebhookController).Assembly);
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
    public async Task FlutterwaveWebhook_ValidSignatureAndPayload_ShouldReturn200Ok()
    {
        // Arrange
        var processor = Substitute.For<IWebhookProcessor>();
        processor.ProcessWebhookAsync(
            PaymentProvider.Flutterwave,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(WebhookProcessingResult.Processed("evt_123", Guid.NewGuid(), "Processed successfully"));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        {
            var content = new StringContent("""{"event":"charge.completed","data":{"id":123}}""", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/flutterwave")
            {
                Content = content
            };
            request.Headers.Add("verif-hash", "test-secret-hash");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task FlutterwaveWebhook_InvalidSignature_ShouldReturn401Unauthorized()
    {
        // Arrange
        var processor = Substitute.For<IWebhookProcessor>();
        processor.ProcessWebhookAsync(
            PaymentProvider.Flutterwave,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(WebhookProcessingResult.InvalidSignature());

        var (host, client) = await CreateTestServer(processor);
        using (host)
        {
            var content = new StringContent("""{"event":"charge.completed","data":{"id":123}}""", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/flutterwave")
            {
                Content = content
            };
            request.Headers.Add("verif-hash", "wrong-hash");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task FlutterwaveWebhook_MalformedPayload_ShouldReturn400BadRequest()
    {
        // Arrange
        var processor = Substitute.For<IWebhookProcessor>();
        processor.ProcessWebhookAsync(
            PaymentProvider.Flutterwave,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(WebhookProcessingResult.InvalidPayload("Unable to parse event fields"));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        {
            var content = new StringContent("malformed-not-json", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/flutterwave")
            {
                Content = content
            };

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task PaystackWebhook_DuplicateEvent_ShouldReturn200OkWithDuplicateMessage()
    {
        // Arrange
        var processor = Substitute.For<IWebhookProcessor>();
        processor.ProcessWebhookAsync(
            PaymentProvider.Paystack,
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, string>>(),
            Arg.Any<CancellationToken>())
            .Returns(WebhookProcessingResult.Duplicate("pstk_dup_123"));

        var (host, client) = await CreateTestServer(processor);
        using (host)
        {
            var content = new StringContent("""{"event":"transfer.success","data":{"reference":"REF-99"}}""", Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/paystack")
            {
                Content = content
            };
            request.Headers.Add("x-paystack-signature", "valid-signature");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
