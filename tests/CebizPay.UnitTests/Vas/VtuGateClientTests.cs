using System.Net;
using System.Text.Json;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Vas.VtuGate;
using CebizPay.Infrastructure.Vas.VtuGate.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Vas;

public class VtuGateClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static VtuGateClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var mockHandler = new MockHttpMessageHandler(handler);
        var httpClient = new HttpClient(mockHandler)
        {
            BaseAddress = new Uri("https://vtugate.com/api/")
        };

        var options = Options.Create(new VtuGateOptions
        {
            Enabled = true,
            BaseUrl = "https://vtugate.com/api",
            ApiKey = "test_api_key_123",
            TimeoutSeconds = 30,
            Environment = "Sandbox"
        });

        return new VtuGateClient(httpClient, options, NullLogger<VtuGateClient>.Instance);
    }

    [Fact]
    public async Task PurchaseAirtimeAsync_WhenSuccessful_ReturnsSuccessResponse()
    {
        // Arrange
        var client = CreateClient(req =>
        {
            Assert.Equal("Bearer", req.Headers.Authorization?.Scheme);
            Assert.Equal("test_api_key_123", req.Headers.Authorization?.Parameter);

            var responseBody = JsonSerializer.Serialize(new VtuGateResponse(
                Status: "success",
                Message: "Airtime top-up successful",
                Reference: "VTU-REF-1001",
                TransactionId: "TXN-9999",
                Code: "00",
                Data: null));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        });

        // Act
        var result = await client.PurchaseAirtimeAsync("CBZVAS-AIR-001", "08031234567", "MTN", 500m);

        // Assert
        Assert.Equal("success", result.Status);
        Assert.Equal("VTU-REF-1001", result.Reference);
    }

    [Fact]
    public async Task PurchaseDataAsync_WhenSuccessful_ReturnsSuccessResponse()
    {
        // Arrange
        var client = CreateClient(req =>
        {
            var responseBody = JsonSerializer.Serialize(new VtuGateResponse(
                Status: "success",
                Message: "Data bundle delivered",
                Reference: "VTU-DAT-2002",
                TransactionId: "TXN-8888",
                Code: "00",
                Data: null));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        });

        // Act
        var result = await client.PurchaseDataAsync("CBZVAS-DAT-002", "08021234567", "AIRTEL", "AIRTEL-1GB", 280m);

        // Assert
        Assert.Equal("success", result.Status);
        Assert.Equal("VTU-DAT-2002", result.Reference);
    }

    [Fact]
    public async Task PurchaseAirtimeAsync_WhenHttp400_ReturnsFailedResponse()
    {
        // Arrange
        var client = CreateClient(_ =>
        {
            var responseBody = JsonSerializer.Serialize(new VtuGateResponse(
                Status: "failed",
                Message: "Invalid mobile number",
                Reference: null,
                TransactionId: null,
                Code: "INVALID_NUMBER",
                Data: null));

            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            };
        });

        // Act
        var result = await client.PurchaseAirtimeAsync("CBZVAS-AIR-003", "08030000000", "MTN", 500m);

        // Assert
        Assert.Equal("failed", result.Status);
        Assert.Equal("INVALID_NUMBER", result.Code);
    }

    [Fact]
    public void MaskPhoneNumber_ProperlyMasksSensitiveDigits()
    {
        var masked = VtuGateClient.MaskPhoneNumber("08031234567");
        Assert.Equal("0803***4567", masked);
    }
}
