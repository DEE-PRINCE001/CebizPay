using CebizPay.Application.Common.Models.Vas;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Commands.PurchaseData;

/// <summary>
/// Command to purchase a mobile data bundle subscription for a recipient phone number.
/// </summary>
public sealed record PurchaseDataCommand(
    string PhoneNumber,
    string Network,
    string ProductCode,
    decimal Amount,
    string TransactionPin,
    string IdempotencyKey,
    Guid? OrganizationContext = null) : IRequest<VasPurchaseResponseDto>;
