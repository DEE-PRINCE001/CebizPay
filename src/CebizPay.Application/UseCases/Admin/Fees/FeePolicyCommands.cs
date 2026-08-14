using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Fees;

/// <summary>
/// Command to create and activate a new peer-transfer fee policy.
/// The previous active policy is deactivated. Historical policies are preserved.
/// Only authorized Super Admin users should invoke this command.
/// </summary>
/// <param name="Mode">Fee mode: Free or Percentage.</param>
/// <param name="PercentageRate">Decimal rate (e.g. 0.01 = 1%). Required for Percentage mode.</param>
/// <param name="MinimumFee">Minimum fee floor. Required for Percentage mode.</param>
/// <param name="MaximumFee">Maximum fee ceiling. Required for Percentage mode. Must be >= MinimumFee.</param>
/// <param name="CreatedByUserId">Super Admin UserId authorizing this change (resolved from auth context by handler).</param>
public sealed record CreateFeePolicyCommand(
    FeePolicyMode Mode,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    string CreatedByUserId) : IRequest<FeePolicyResponseDto>;

/// <summary>
/// Query to retrieve all fee policies ordered by version descending.
/// </summary>
public sealed record GetAllFeePoliciesQuery : IRequest<IReadOnlyList<FeePolicyResponseDto>>;

/// <summary>
/// Query to retrieve the current active fee policy.
/// </summary>
public sealed record GetActiveFeePolicyQuery : IRequest<FeePolicyResponseDto?>;

/// <summary>
/// DTO representing a peer-transfer fee policy response.
/// </summary>
public sealed record FeePolicyResponseDto(
    Guid Id,
    string Mode,
    decimal? PercentageRate,
    decimal? MinimumFee,
    decimal? MaximumFee,
    bool IsEnabled,
    int Version,
    string CreatedByUserId,
    DateTime EffectiveFrom,
    DateTime CreatedAtUtc,
    DateTime? DeactivatedAtUtc);
