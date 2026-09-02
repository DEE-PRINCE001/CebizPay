using System.Net;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CebizPay.UnitTests.Messaging;

public sealed class TwilioSmsServiceTests
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
    public async Task SendSmsAsync_WhenDisabled_ShouldLogAndReturnTrueWithoutHttpCall()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new TwilioOptions
        {
            Enabled = false,
            AccountSid = "",
            AuthToken = ""
        });

        var service = new TwilioSmsService(httpClient, options, NullLogger<TwilioSmsService>.Instance);

        // Act
        var result = await service.SendSmsAsync("+2348012345678", "Your code is 123456");

        // Assert
        Assert.True(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SendSmsAsync_WhenEmptyPhoneOrMessage_ShouldReturnFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new TwilioOptions
        {
            Enabled = true,
            AccountSid = "AC123",
            AuthToken = "token"
        });

        var service = new TwilioSmsService(httpClient, options, NullLogger<TwilioSmsService>.Instance);

        // Act & Assert
        Assert.False(await service.SendSmsAsync("", "Hello"));
        Assert.False(await service.SendSmsAsync("+2348012345678", ""));
    }

    [Fact]
    public async Task SendSmsAsync_WhenEnabledAndApiReturns201_ShouldReturnTrue()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.Created, "{\"sid\":\"SM123\",\"status\":\"queued\"}");
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new TwilioOptions
        {
            Enabled = true,
            AccountSid = "AC_TEST_ACCOUNT_SID",
            AuthToken = "AUTH_TOKEN_TEST",
            FromPhoneNumber = "+1234567890"
        });

        var service = new TwilioSmsService(httpClient, options, NullLogger<TwilioSmsService>.Instance);

        // Act
        var result = await service.SendSmsAsync("+2348012345678", "Your verification code is 654321");

        // Assert
        Assert.True(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Basic", handler.LastRequest.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task SendSmsAsync_WhenApiReturnsError_ShouldReturnFalse()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, "{\"code\":21211,\"message\":\"Invalid 'To' Phone Number\"}");
        var httpClient = new HttpClient(handler);
        var options = Microsoft.Extensions.Options.Options.Create(new TwilioOptions
        {
            Enabled = true,
            AccountSid = "AC_TEST_ACCOUNT_SID",
            AuthToken = "AUTH_TOKEN_TEST"
        });

        var service = new TwilioSmsService(httpClient, options, NullLogger<TwilioSmsService>.Instance);

        // Act
        var result = await service.SendSmsAsync("+2340000000000", "Your code");

        // Assert
        Assert.False(result);
    }
}
