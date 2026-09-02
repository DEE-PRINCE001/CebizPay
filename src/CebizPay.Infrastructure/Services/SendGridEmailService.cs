#pragma warning disable CA1848, CA1873
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure service for sending transactional emails via the SendGrid v3 REST API,
/// with dev-mode logger fallback when SendGrid is unconfigured or disabled.
/// </summary>
public sealed partial class SendGridEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly SendGridOptions _options;
    private readonly ILogger<SendGridEmailService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of <see cref="SendGridEmailService"/>.
    /// </summary>
    public SendGridEmailService(
        HttpClient httpClient,
        IOptions<SendGridOptions> options,
        ILogger<SendGridEmailService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.sendgrid.com/");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string? plainTextBody = null,
        string? toName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("SendEmailAsync called with empty recipient email address.");
            return false;
        }

        // Development/Offline Fallback: If SendGrid is disabled or ApiKey is missing, log email and succeed
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation(
                "[EMAIL-DEV] To: {ToEmail} ({ToName}), Subject: '{Subject}'\n--- HTML CONTENT ---\n{HtmlBody}\n---------------------",
                toEmail, toName ?? "N/A", subject, htmlBody);
            return true;
        }

        try
        {
            var contentList = new List<SendGridContentItem>();

            if (!string.IsNullOrWhiteSpace(plainTextBody))
            {
                contentList.Add(new SendGridContentItem("text/plain", plainTextBody));
            }

            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                contentList.Add(new SendGridContentItem("text/html", htmlBody));
            }

            if (contentList.Count == 0)
            {
                contentList.Add(new SendGridContentItem("text/plain", subject));
            }

            var toList = new List<SendGridEmailAddress>
            {
                new(toEmail.Trim(), string.IsNullOrWhiteSpace(toName) ? null : toName.Trim())
            };

            var payload = new SendGridMailSendRequest
            {
                Personalizations = new List<SendGridPersonalization>
                {
                    new(toList, subject)
                },
                From = new SendGridEmailAddress(
                    string.IsNullOrWhiteSpace(_options.FromEmail) ? "noreply@cebizpay.com" : _options.FromEmail.Trim(),
                    string.IsNullOrWhiteSpace(_options.FromName) ? "CebizPay" : _options.FromName.Trim()),
                Subject = subject,
                Content = contentList
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email successfully dispatched via SendGrid to {ToEmail}. Subject: '{Subject}'", toEmail, subject);
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "SendGrid API returned status {StatusCode} when sending email to {ToEmail}. Error: {Error}",
                (int)response.StatusCode, toEmail, errorContent);

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception sending email to {ToEmail} via SendGrid: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    private sealed class SendGridMailSendRequest
    {
        [JsonPropertyName("personalizations")]
        public List<SendGridPersonalization> Personalizations { get; set; } = new();

        [JsonPropertyName("from")]
        public SendGridEmailAddress From { get; set; } = null!;

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<SendGridContentItem> Content { get; set; } = new();
    }

    private sealed class SendGridPersonalization
    {
        [JsonPropertyName("to")]
        public List<SendGridEmailAddress> To { get; set; } = new();

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        public SendGridPersonalization(List<SendGridEmailAddress> to, string? subject = null)
        {
            To = to;
            Subject = subject;
        }
    }

    private sealed class SendGridEmailAddress
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        public SendGridEmailAddress(string email, string? name = null)
        {
            Email = email;
            Name = name;
        }
    }

    private sealed class SendGridContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        public SendGridContentItem(string type, string value)
        {
            Type = type;
            Value = value;
        }
    }
}
