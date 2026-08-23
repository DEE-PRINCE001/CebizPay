using CebizPay.Application.Common.Models.Vas;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.GetVasTransactionById;

/// <summary>
/// Query to retrieve details of a specific VAS transaction by identifier.
/// </summary>
public sealed record GetVasTransactionByIdQuery(
    Guid Id,
    Guid? OrganizationContext = null) : IRequest<VasTransactionResponseDto>;
