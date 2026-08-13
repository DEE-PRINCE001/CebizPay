using CebizPay.Application.Common.Interfaces.Security;

namespace CebizPay.IntegrationTests.Security;

/// <summary>
/// Test-only implementation of <see cref="IMfaCodeDeliveryService"/> that captures
/// the delivered MFA code in memory for test assertion purposes.
/// <para>
/// This exists ONLY in the test project. It is never registered in production DI.
/// The production system uses <c>NoOpMfaCodeDeliveryService</c> until a concrete
/// factor is selected.
/// </para>
/// <para>
/// Security note: tests must NOT assert that the code is available through any
/// production API surface — only through this internal spy.
/// </para>
/// </summary>
public sealed class TestMfaCodeDeliveryService : IMfaCodeDeliveryService
{
    // Maps userId → most recently delivered code for that user.
    private readonly Dictionary<string, string> _deliveredCodes = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task DeliverAsync(string userId, string plainCode, CancellationToken cancellationToken = default)
    {
        // Capture the code for test inspection — NOT for production use.
        _deliveredCodes[userId] = plainCode;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the most recently delivered code for the given user, or null if none was delivered.
    /// Only available in the test project.
    /// </summary>
    public string? GetDeliveredCode(string userId)
        => _deliveredCodes.TryGetValue(userId, out var code) ? code : null;
}
