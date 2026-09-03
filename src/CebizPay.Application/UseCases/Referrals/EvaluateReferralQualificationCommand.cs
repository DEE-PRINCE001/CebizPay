using CebizPay.Application.Common.Interfaces.Referrals;
using MediatR;

namespace CebizPay.Application.UseCases.Referrals;

/// <summary>
/// Command to trigger milestone qualification evaluation for a user's referral relationship.
/// </summary>
public sealed record EvaluateReferralQualificationCommand(
    string UserId) : IRequest<ReferralQualificationEvaluationResult>;

/// <summary>
/// Handler for EvaluateReferralQualificationCommand.
/// </summary>
public sealed class EvaluateReferralQualificationCommandHandler : IRequestHandler<EvaluateReferralQualificationCommand, ReferralQualificationEvaluationResult>
{
    private readonly IReferralQualificationService _qualificationService;

    /// <summary>
    /// Initializes a new instance of <see cref="EvaluateReferralQualificationCommandHandler"/>.
    /// </summary>
    public EvaluateReferralQualificationCommandHandler(IReferralQualificationService qualificationService)
    {
        _qualificationService = qualificationService ?? throw new ArgumentNullException(nameof(qualificationService));
    }

    /// <inheritdoc/>
    public Task<ReferralQualificationEvaluationResult> Handle(EvaluateReferralQualificationCommand request, CancellationToken cancellationToken)
    {
        return _qualificationService.EvaluateQualificationAsync(request.UserId, cancellationToken);
    }
}
