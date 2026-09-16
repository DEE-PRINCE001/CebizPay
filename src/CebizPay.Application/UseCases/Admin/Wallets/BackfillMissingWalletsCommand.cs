using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Finance;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Domain.Finance.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Admin.Wallets;

/// <summary>
/// Response payload for administrative wallet backfill operation.
/// </summary>
/// <param name="IndividualWalletsCreated">Number of individual wallets created.</param>
/// <param name="OrganizationWalletsCreated">Number of corporate organization wallets created.</param>
/// <param name="Message">Status description message.</param>
public sealed record BackfillMissingWalletsResponseDto(
    int IndividualWalletsCreated,
    int OrganizationWalletsCreated,
    string Message);

/// <summary>
/// Command to scan and automatically provision missing wallets for registered individuals and organizations.
/// </summary>
/// <param name="Currency">Target currency to backfill (defaults to NGN).</param>
public sealed record BackfillMissingWalletsCommand(
    Currency Currency = Currency.NGN) : IRequest<BackfillMissingWalletsResponseDto>;

/// <summary>
/// Validator for <see cref="BackfillMissingWalletsCommand"/>.
/// </summary>
public sealed class BackfillMissingWalletsCommandValidator : AbstractValidator<BackfillMissingWalletsCommand>
{
    /// <summary>
    /// Initializes validation rules for BackfillMissingWalletsCommand.
    /// </summary>
    public BackfillMissingWalletsCommandValidator()
    {
        RuleFor(x => x.Currency)
            .Must(c => c == Currency.NGN || c == Currency.USDT || c == Currency.INTERNATIONAL_NGN)
            .WithMessage("Currency must be a valid transactional V1 currency (NGN, USDT, or INTERNATIONAL_NGN).");
    }
}

/// <summary>
/// Handler for <see cref="BackfillMissingWalletsCommand"/>.
/// </summary>
public sealed class BackfillMissingWalletsCommandHandler
    : IRequestHandler<BackfillMissingWalletsCommand, BackfillMissingWalletsResponseDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IWalletService _walletService;

    /// <summary>
    /// Initializes a new instance of <see cref="BackfillMissingWalletsCommandHandler"/>.
    /// </summary>
    public BackfillMissingWalletsCommandHandler(
        IApplicationDbContext dbContext,
        IWalletService walletService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _walletService = walletService ?? throw new ArgumentNullException(nameof(walletService));
    }

    /// <inheritdoc/>
    public async Task<BackfillMissingWalletsResponseDto> Handle(
        BackfillMissingWalletsCommand request,
        CancellationToken cancellationToken)
    {
        request.Currency.EnsureTransactionalV1();

        // 1. Resolve individual profiles missing a wallet in the requested currency
        var existingIndividualWalletUserIds = await _dbContext.Wallets
            .Where(w => w.IndividualId != null && w.Currency == request.Currency)
            .Select(w => w.IndividualId!)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missingUserIds = await _dbContext.IndividualProfiles
            .Where(p => !existingIndividualWalletUserIds.Contains(p.UserId))
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var individualCreatedCount = 0;
        foreach (var userId in missingUserIds)
        {
            await _walletService.GetOrCreateIndividualWalletAsync(userId, request.Currency, cancellationToken)
                .ConfigureAwait(false);
            individualCreatedCount++;
        }

        // 2. Resolve active organizations missing a corporate wallet in the requested currency
        var existingOrgWalletOrgIds = await _dbContext.Wallets
            .Where(w => w.OrganizationId != null && w.Currency == request.Currency)
            .Select(w => w.OrganizationId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var missingOrgIds = await _dbContext.Organizations
            .Where(o => !o.IsDeleted && !existingOrgWalletOrgIds.Contains(o.Id))
            .Select(o => o.Id)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var orgCreatedCount = 0;
        foreach (var orgId in missingOrgIds)
        {
            await _walletService.GetOrCreateOrganizationWalletAsync(orgId, request.Currency, cancellationToken)
                .ConfigureAwait(false);
            orgCreatedCount++;
        }

        return new BackfillMissingWalletsResponseDto(
            IndividualWalletsCreated: individualCreatedCount,
            OrganizationWalletsCreated: orgCreatedCount,
            Message: $"Successfully backfilled {individualCreatedCount} individual wallet(s) and {orgCreatedCount} organization wallet(s) for {request.Currency}.");
    }
}
