using System.Net;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CebizPay.UnitTests.Messaging;

public sealed class SendGridEmailServiceTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;
        public HttpRequestMessage? LastRequest { get; private set; }

        public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent = "")
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task SendEmailAsync_WhenDisabled_ShouldLogAndReturnTrueWithoutHttpCall()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new SendGridOptions
        {
            Enabled = false,
            ApiKey = ""
        });

        var service = new SendGridEmailService(httpClient, options, NullLogger<SendGridEmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("test@example.com", "Test Subject", "<p>Hello</p>");

        // Assert
        Assert.True(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_WhenEmptyEmail_ShouldReturnFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "SG.test"
        });

        var service = new SendGridEmailService(httpClient, options, NullLogger<SendGridEmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("", "Test Subject", "<p>Hello</p>");

        // Assert
        Assert.False(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_WhenEnabledAndApiReturns202_ShouldReturnTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.Accepted, "");
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "SG.real_api_key_test",
            FromEmail = "noreply@cebizpay.com",
            FromName = "CebizPay"
        });

        var service = new SendGridEmailService(httpClient, options, NullLogger<SendGridEmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("user@example.com", "Welcome", "<h1>Welcome!</h1>", "Welcome!", "Test User");

        // Assert
        Assert.True(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("SG.real_api_key_test", handler.LastRequest.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiReturnsError_ShouldReturnFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.Unauthorized, "{\"errors\":[{\"message\":\"The provided authorization grant is invalid\"}]}");
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new SendGridOptions
        {
            Enabled = true,
            ApiKey = "SG.invalid_key"
        });

        var service = new SendGridEmailService(httpClient, options, NullLogger<SendGridEmailService>.Instance);

        // Act
        var result = await service.SendEmailAsync("user@example.com", "Test", "<p>Test</p>");

        // Assert
        Assert.False(result);
    }
}
