using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Referrals;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Referrals.Entities;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Command to retrieve the authenticated user's active referral code, creating one if not present.
/// </summary>
public sealed record GetOrCreateReferralCodeCommand : IRequest<string>;

/// <summary>
/// Handler for GetOrCreateReferralCodeCommand.
/// </summary>
public sealed class GetOrCreateReferralCodeCommandHandler : IRequestHandler<GetOrCreateReferralCodeCommand, string>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IReferralCodeGenerator _codeGenerator;

    /// <summary>
    /// Initializes a new instance of <see cref="GetOrCreateReferralCodeCommandHandler"/>.
    /// </summary>
    public GetOrCreateReferralCodeCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IReferralCodeGenerator codeGenerator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _codeGenerator = codeGenerator ?? throw new ArgumentNullException(nameof(codeGenerator));
    }

    /// <inheritdoc/>
    public async Task<string> Handle(GetOrCreateReferralCodeCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var existing = await _dbContext.ReferralCodes
            .FirstOrDefaultAsync(c => c.UserId == callerUserId && c.IsActive, cancellationToken);

        if (existing != null)
        {
            return existing.Code;
        }

        var now = DateTime.UtcNow;
        string uniqueCode;

        // Generate collision-resistant unique code
        while (true)
        {
            uniqueCode = _codeGenerator.GenerateCode();
            var exists = await _dbContext.ReferralCodes
                .AnyAsync(c => c.Code == uniqueCode, cancellationToken);

            if (!exists)
            {
                break;
            }
        }

        var referralCode = ReferralCode.Create(callerUserId, uniqueCode, now);
        _dbContext.ReferralCodes.Add(referralCode);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return referralCode.Code;
    }
}
