using CebizPay.Domain.Finance.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Treasury;

/// <summary>
/// Query to retrieve the platform master treasury and liquidity summary for a given currency.
/// </summary>
public sealed record GetPlatformTreasurySummaryQuery(
    Currency Currency = Currency.NGN) : IRequest<PlatformTreasurySummaryDto>;
