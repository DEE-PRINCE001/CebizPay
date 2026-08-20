using System.Net;
using System.Text;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Reusable test mock HTTP message handler returning canned HTTP responses.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseContent;
    private readonly bool _simulateTimeout;

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
    {
        _statusCode = statusCode;
        _responseContent = responseContent;
        _simulateTimeout = false;
    }

    public MockHttpMessageHandler(bool simulateTimeout)
    {
        _statusCode = HttpStatusCode.RequestTimeout;
        _responseContent = string.Empty;
        _simulateTimeout = simulateTimeout;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_simulateTimeout)
        {
            throw new OperationCanceledException("Simulated HTTP request timeout.");
        }

        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
        };

        return Task.FromResult(response);
    }
}
