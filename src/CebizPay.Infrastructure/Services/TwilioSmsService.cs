#pragma warning disable CA1848, CA1873
using System.Net.Http.Headers;
using System.Text;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service for sending SMS messages via the Twilio REST API,
/// with dev-mode logger fallback when Twilio is unconfigured or disabled.
/// </summary>
public sealed class TwilioSmsService : ISmsService
{
    private readonly HttpClient _httpClient;
    private readonly TwilioOptions _options;
    private readonly ILogger<TwilioSmsService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TwilioSmsService"/>.
    /// </summary>
    public TwilioSmsService(
        HttpClient httpClient,
        IOptions<TwilioOptions> options,
        ILogger<TwilioSmsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.twilio.com/");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendSmsAsync(
        string toPhoneNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber) || string.IsNullOrWhiteSpace(message))
        {
            _logger.LogWarning("SendSmsAsync called with empty recipient phone number or message.");
            return false;
        }

        var cleanPhone = toPhoneNumber.Trim();

        // Development/Offline Fallback: If Twilio is disabled or credentials missing, log SMS and succeed
        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.AccountSid) ||
            string.IsNullOrWhiteSpace(_options.AuthToken))
        {
            _logger.LogInformation(
                "[SMS-DEV] To: {ToPhone}, Message: '{Message}'",
                cleanPhone, message);
            return true;
        }

        try
        {
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("To", cleanPhone),
                new("Body", message.Trim())
            };

            if (!string.IsNullOrWhiteSpace(_options.MessagingServiceSid))
            {
                formValues.Add(new("MessagingServiceSid", _options.MessagingServiceSid.Trim()));
            }
            else if (!string.IsNullOrWhiteSpace(_options.FromPhoneNumber))
            {
                formValues.Add(new("From", _options.FromPhoneNumber.Trim()));
            }

            var requestUri = $"2010-04-01/Accounts/{_options.AccountSid.Trim()}/Messages.json";
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new FormUrlEncodedContent(formValues)
            };

            var authBytes = Encoding.ASCII.GetBytes($"{_options.AccountSid.Trim()}:{_options.AuthToken.Trim()}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SMS successfully dispatched via Twilio to {ToPhone}.", cleanPhone);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Twilio API returned status {StatusCode} when sending SMS to {ToPhone}. Error: {Error}",
                (int)response.StatusCode, cleanPhone, errorContent);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending SMS to {ToPhone} via Twilio: {Message}", cleanPhone, ex.Message);
            return false;
        }
    }
}
