using CebizPay.Application.Common.Exceptions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Domain.Enums;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.GetVasTransactionById;

/// <summary>
/// Handles <see cref="GetVasTransactionByIdQuery"/>.
/// Enforces multi-tenant data isolation and role-based access control.
/// </summary>
public sealed class GetVasTransactionByIdQueryHandler : IRequestHandler<GetVasTransactionByIdQuery, VasTransactionResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="GetVasTransactionByIdQueryHandler"/>.
    /// </summary>
    public GetVasTransactionByIdQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc/>
    public async Task<VasTransactionResponseDto> Handle(GetVasTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required to query VAS transactions.");

        var txn = await _dbContext.VasTransactions
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"VAS transaction '{request.Id}' was not found.");

        // Multi-tenant & ownership authorization check
        if (txn.OrganizationId.HasValue)
        {
            var membership = await _dbContext.OrganizationMemberships
                .FirstOrDefaultAsync(m => m.OrganizationId == txn.OrganizationId.Value && m.UserId == userId && m.Status == MembershipStatus.Active, cancellationToken)
                ?? throw new UnauthorizedAccessException("You do not have access to view this organization's VAS transaction.");

            if (!membership.HasPermission(Domain.Permissions.Permissions.VasView) &&
                !membership.HasPermission(Domain.Permissions.Permissions.TransactionsView) &&
                !membership.HasPermission(Domain.Permissions.Permissions.WalletView))
            {
                throw new UnauthorizedAccessException("Insufficient permissions to view VAS transactions.");
            }
        }
        else
        {
            if (txn.UserId != userId)
            {
                throw new UnauthorizedAccessException("You do not have access to view this personal VAS transaction.");
            }
        }

        return new VasTransactionResponseDto(
            Id: txn.Id,
            Reference: txn.Reference,
            Type: txn.Type.ToString().ToUpperInvariant(),
            Status: txn.Status.ToString().ToUpperInvariant(),
            Amount: txn.Amount,
            Currency: txn.Currency.ToString(),
            Network: txn.Network.ToString().ToUpperInvariant(),
            MaskedPhoneNumber: txn.GetMaskedPhoneNumber(),
            ProductCode: txn.ProductCode,
            ProductName: txn.ProductName,
            ProviderReference: txn.ProviderReference,
            CreatedAtUtc: txn.CreatedAtUtc,
            CompletedAtUtc: txn.CompletedAtUtc,
            ReversedAtUtc: txn.ReversedAtUtc,
            FailureReason: txn.FailureReason);
    }
}
