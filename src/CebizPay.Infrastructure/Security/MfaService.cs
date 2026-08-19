using System.Security.Cryptography;
using System.Text;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Security;

/// <summary>
/// Infrastructure service implementing short-lived, single-use, rate-limited MFA challenges and MFA status management.
/// <para>
/// Security contract:
/// <list type="bullet">
/// <item>Challenge codes are generated using a cryptographically-secure random number generator.</item>
/// <item>The code is immediately SHA-256 hashed before persistence.</item>
/// <item>The raw code is passed to <see cref="IMfaCodeDeliveryService"/> once and then discarded.</item>
/// <item>The raw code is NEVER returned from <see cref="CreateChallengeAsync"/> or stored in plain form.</item>
/// <item>The raw code is NEVER logged, placed in error messages, or exposed through telemetry.</item>
/// </list>
/// </para>
/// </summary>
public sealed class MfaService : IMfaService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IMfaCodeDeliveryService _deliveryService;

    /// <summary>
    /// Initializes a new instance of <see cref="MfaService"/>.
    /// </summary>
    public MfaService(IApplicationDbContext dbContext, IMfaCodeDeliveryService deliveryService)
    {
        _dbContext = dbContext;
        _deliveryService = deliveryService;
    }

    /// <inheritdoc/>
    public async Task<bool> IsMfaEnabledAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;

        var admin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        return admin?.IsMfaEnabled ?? false;
    }

    /// <inheritdoc/>
    public async Task EnableMfaAsync(string userId, CancellationToken cancellationToken = default)
    {
        var admin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (admin != null)
        {
            admin.SetMfaStatus(true);
            _dbContext.AuditLogs.Add(Domain.Entities.AuditLog.Create(
                actorId: userId,
                action: Domain.Auditing.AuditActions.MfaEnabled,
                resourceType: Domain.Auditing.AuditResourceTypes.AdminProfile,
                resourceId: admin.Id.ToString()));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task DisableMfaAsync(string userId, CancellationToken cancellationToken = default)
    {
        var admin = await _dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

        if (admin != null)
        {
            admin.SetMfaStatus(false);
            _dbContext.AuditLogs.Add(Domain.Entities.AuditLog.Create(
                actorId: userId,
                action: Domain.Auditing.AuditActions.MfaDisabled,
                resourceType: Domain.Auditing.AuditResourceTypes.AdminProfile,
                resourceId: admin.Id.ToString()));
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The raw 6-digit code is generated, hashed, persisted, and then passed to
    /// <see cref="IMfaCodeDeliveryService.DeliverAsync"/> before being discarded from this scope.
    /// Only the <see cref="MfaChallenge.Id"/> and expiry are returned — the code is never surfaced.
    /// </remarks>
    public async Task<(Guid ChallengeId, DateTime ExpiresAtUtc)> CreateChallengeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("UserId is required.", nameof(userId));

        // Generate 6-digit random code using a cryptographically-secure RNG
        var codeInt = RandomNumberGenerator.GetInt32(100000, 1000000);
        var plainCode = codeInt.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);

        // Hash immediately — the plain code lives only in this stack frame
        var codeHash = ComputeHash(plainCode);

        // Challenge expires in 5 minutes
        var challenge = new MfaChallenge(userId, codeHash, TimeSpan.FromMinutes(5));
        _dbContext.MfaChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Delegate delivery to the configured channel abstraction.
        // The plain code is passed here ONLY for delivery and is not stored again.
        await _deliveryService.DeliverAsync(userId, plainCode, cancellationToken);

        // Return only the challenge identifier and expiry — never the plain code.
        return (challenge.Id, challenge.ExpiresAtUtc);
    }

    /// <inheritdoc/>
    public async Task<(bool Succeeded, string? UserId, IEnumerable<string> Errors)> VerifyChallengeAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty)
            return (false, null, new[] { "Invalid challenge ID." });
        if (string.IsNullOrWhiteSpace(code))
            return (false, null, new[] { "MFA code is required." });

        var challenge = await _dbContext.MfaChallenges
            .FirstOrDefaultAsync(c => c.Id == challengeId, cancellationToken);

        if (challenge == null)
        {
            return (false, null, new[] { "MFA challenge not found." });
        }

        if (challenge.IsUsed)
        {
            return (false, null, new[] { "MFA challenge has already been used." });
        }

        if (challenge.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return (false, null, new[] { "MFA challenge has expired." });
        }

        // Rate limiting check: maximum 3 failed attempts
        if (challenge.FailedAttempts >= 3)
        {
            challenge.MarkUsed();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (false, null, new[] { "Excessive failed MFA attempts. Challenge invalidated." });
        }

        var inputHash = ComputeHash(code.Trim());
        if (inputHash != challenge.CodeHash)
        {
            challenge.IncrementFailedAttempts();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return (false, null, new[] { "Invalid MFA verification code." });
        }

        // Verification succeeded - mark challenge as used (single-use constraint)
        challenge.MarkUsed();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (true, challenge.UserId, Array.Empty<string>());
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
