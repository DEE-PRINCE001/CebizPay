#pragma warning disable CA1848, CA1873
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CebizPay.Infrastructure.Security;

/// <summary>
/// Production and development implementation of <see cref="IMfaCodeDeliveryService"/>
/// that dispatches MFA challenge codes via SendGrid Email and Twilio SMS.
/// </summary>
public sealed class EmailAndSmsMfaCodeDeliveryService : IMfaCodeDeliveryService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly ILogger<EmailAndSmsMfaCodeDeliveryService> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EmailAndSmsMfaCodeDeliveryService"/>.
    /// </summary>
    public EmailAndSmsMfaCodeDeliveryService(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ISmsService smsService,
        ILogger<EmailAndSmsMfaCodeDeliveryService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _smsService = smsService ?? throw new ArgumentNullException(nameof(smsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task DeliverAsync(string userId, string plainCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(plainCode))
        {
            return;
        }

        // Look up user details
        var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);

        var email = user?.Email;
        var phone = user?.PhoneNumber;

        _logger.LogInformation("[MFA-DEV] Generated MFA challenge code for user {UserId} ({Email}).", userId, email ?? "N/A");

        // 1. Dispatch Email via SendGrid
        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailSubject = "CebizPay — Multi-Factor Authentication Code";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; padding: 24px; }}
        .container {{ max-width: 540px; margin: 0 auto; background: #ffffff; border-radius: 12px; border: 1px solid #e2e8f0; padding: 32px; }}
        .header {{ font-size: 20px; font-weight: 700; color: #0f172a; margin-bottom: 16px; }}
        .code-box {{ background-color: #f1f5f9; border-radius: 8px; font-size: 32px; font-weight: 800; letter-spacing: 6px; text-align: center; padding: 18px; margin: 24px 0; color: #0284c7; border: 1px dashed #cbd5e1; }}
        .footer {{ font-size: 12px; color: #64748b; margin-top: 32px; border-top: 1px solid #f1f5f9; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>CebizPay Security Verification</div>
        <p>You have requested a multi-factor authentication security verification code to sign into your CebizPay account.</p>
        <div class='code-box'>{plainCode}</div>
        <p>This verification code is valid for <strong>5 minutes</strong>. If you did not initiate this request, please contact support immediately.</p>
        <div class='footer'>&copy; {DateTime.UtcNow.Year} CebizPay. All rights reserved. Confidential security communication.</div>
    </div>
</body>
</html>";

            var plainText = $"Your CebizPay MFA security code is: {plainCode}. Valid for 5 minutes.";
            await _emailService.SendEmailAsync(email, emailSubject, htmlBody, plainText, toName: null, cancellationToken).ConfigureAwait(false);
        }

        // 2. Dispatch SMS via Twilio
        if (!string.IsNullOrWhiteSpace(phone))
        {
            var smsText = $"Your CebizPay MFA security code is: {plainCode}. Valid for 5 minutes. Do not share this code.";
            await _smsService.SendSmsAsync(phone, smsText, cancellationToken).ConfigureAwait(false);
        }
    }
}
