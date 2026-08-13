namespace CebizPay.Application.Common.Interfaces.Security;

/// <summary>
/// Contract for multi-factor authentication (MFA) foundation services.
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Checks if MFA is enabled for a given user.
    /// </summary>
    Task<bool> IsMfaEnabledAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables MFA for a user.
    /// </summary>
    Task EnableMfaAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables MFA for a user.
    /// </summary>
    Task DisableMfaAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a short-lived MFA challenge for authentication continuation.
    /// The generated code is hashed and persisted, then delegated to
    /// <see cref="IMfaCodeDeliveryService"/> for delivery through the configured channel.
    /// The raw code is NEVER returned to the caller — only the challenge identifier and metadata.
    /// </summary>
    Task<(Guid ChallengeId, DateTime ExpiresAtUtc)> CreateChallengeAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a short-lived MFA challenge. Returns success result, target userId, or error messages.
    /// </summary>
    Task<(bool Succeeded, string? UserId, IEnumerable<string> Errors)> VerifyChallengeAsync(Guid challengeId, string code, CancellationToken cancellationToken = default);
}
