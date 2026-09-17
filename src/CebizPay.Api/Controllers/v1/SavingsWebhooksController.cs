#pragma warning disable CA1848, CS1591
using System.Text;
using Asp.Versioning;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Savings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CebizPay.Api.Controllers.v1;

/// <summary>
/// External savings and wealth provider webhook ingestion endpoints.
/// Ingests, authenticates, and normalizes notifications from Cowrywise, Anchor, and other providers.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/savings/webhooks")]
[AllowAnonymous]
public sealed class SavingsWebhooksController : ControllerBase
{
    private readonly ISavingsProviderFactory _providerFactory;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<SavingsWebhooksController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavingsWebhooksController"/> class.
    /// </summary>
    public SavingsWebhooksController(
        ISavingsProviderFactory providerFactory,
        IApplicationDbContext dbContext,
        ILogger<SavingsWebhooksController> logger)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Ingests Cowrywise savings webhooks.
    /// </summary>
    [HttpPost("cowrywise")]
    public Task<IActionResult> CowrywiseWebhook(CancellationToken cancellationToken) =>
        ProcessWebhookInternalAsync("Cowrywise", cancellationToken);

    /// <summary>
    /// Ingests Anchor savings / sub-account webhooks.
    /// </summary>
    [HttpPost("anchor")]
    public Task<IActionResult> AnchorWebhook(CancellationToken cancellationToken) =>
        ProcessWebhookInternalAsync("Anchor", cancellationToken);

    /// <summary>
    /// Ingests generic external savings provider webhooks.
    /// </summary>
    [HttpPost("{provider}")]
    public Task<IActionResult> GenericProviderWebhook([FromRoute] string provider, CancellationToken cancellationToken) =>
        ProcessWebhookInternalAsync(provider, cancellationToken);

    private async Task<IActionResult> ProcessWebhookInternalAsync(string providerName, CancellationToken cancellationToken)
    {
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogWarning("Received empty savings webhook for provider {Provider}.", providerName);
            return BadRequest(new { error = "Empty body" });
        }

        var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);

        ISavingsProvider provider;
        try
        {
            provider = _providerFactory.GetProvider(providerName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Savings provider '{Provider}' not recognized.", providerName);
            return NotFound(new { error = $"Provider '{providerName}' not found" });
        }

        var webhookEvent = await provider.ParseWebhookAsync(rawBody, headers, cancellationToken);
        if (webhookEvent == null)
        {
            _logger.LogWarning("Savings webhook verification or parsing failed for provider {Provider}.", providerName);
            return Unauthorized(new { error = "Invalid signature or malformed payload" });
        }

        if (!string.IsNullOrWhiteSpace(webhookEvent.ExternalPlanId))
        {
            var account = await _dbContext.SavingsAccounts
                .FirstOrDefaultAsync(a => a.ExternalPlanId == webhookEvent.ExternalPlanId, cancellationToken);

            if (account != null)
            {
                try
                {
                    var position = await provider.GetPositionAsync(webhookEvent.ExternalPlanId, cancellationToken);
                    if (position != null)
                    {
                        var incrementalInterest = position.AccruedInterest - account.AccruedInterest;
                        if (incrementalInterest > 0)
                        {
                            var accrual = account.AccrueDailyInterest(incrementalInterest, DateTime.UtcNow.Date);
                            if (accrual != null)
                            {
                                _dbContext.SavingsInterestAccruals.Add(accrual);
                            }
                        }

                        account.SyncExternalYield(
                            position.AccruedInterest,
                            position.PrincipalBalance > 0 ? position.PrincipalBalance : account.PrincipalBalance,
                            position.IsMatured,
                            position.ExternalStatus,
                            DateTime.UtcNow);

                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to apply webhook state for account {AccountId}.", account.Id);
                }
            }
        }

        return Ok(new { received = true, eventType = webhookEvent.EventType, eventId = webhookEvent.RawEventId });
    }
}
