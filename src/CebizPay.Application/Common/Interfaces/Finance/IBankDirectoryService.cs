namespace CebizPay.Application.Common.Interfaces.Finance;

/// <summary>
/// Representation of a Nigerian financial institution for transfer resolution and routing.
/// </summary>
public sealed record BankDto(
    string Name,
    string Code,
    string? Slug = null,
    string? LongCode = null,
    string? Gateway = null,
    bool? Active = true);

/// <summary>
/// Service providing a cached directory of commercial and digital banks in Nigeria.
/// </summary>
public interface IBankDirectoryService
{
    /// <summary>
    /// Retrieves all active commercial and digital banks supported for outbound transfers.
    /// </summary>
    Task<IReadOnlyList<BankDto>> GetBanksAsync(CancellationToken cancellationToken = default);
}
