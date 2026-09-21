namespace CebizPay.Application.Common.Interfaces.Compliance;

/// <summary>
/// Result DTO for on-demand KYC status synchronization.
/// </summary>
public sealed record KycSyncResultDto(
    string ReferenceId,
    string Status,
    string? Message,
    string? KycTier = null,
    string? VirtualAccountNumber = null,
    string? BankName = null);
