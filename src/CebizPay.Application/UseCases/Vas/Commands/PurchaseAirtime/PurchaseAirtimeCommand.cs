using CebizPay.Application.Common.Models.Vas;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Commands.PurchaseAirtime;

/// <summary>
/// Command to purchase prepaid mobile airtime top-up for a recipient phone number.
/// </summary>
public sealed record PurchaseAirtimeCommand(
    string PhoneNumber,
    string? Network,
    decimal Amount,
    string TransactionPin,
    string IdempotencyKey,
    Guid? OrganizationContext = null) : IRequest<VasPurchaseResponseDto>;
