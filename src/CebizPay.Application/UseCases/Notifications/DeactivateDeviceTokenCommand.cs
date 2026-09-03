using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to deactivate a device token upon logout or unregistration.
/// </summary>
public sealed record DeactivateDeviceTokenCommand(
    string Token) : IRequest<bool>;

/// <summary>
/// Validator for DeactivateDeviceTokenCommand.
/// </summary>
public sealed class DeactivateDeviceTokenCommandValidator : AbstractValidator<DeactivateDeviceTokenCommand>
{
    /// <summary>
    /// Initializes validation rules for DeactivateDeviceTokenCommand.
    /// </summary>
    public DeactivateDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Device token is required.");
    }
}

/// <summary>
/// Handler for DeactivateDeviceTokenCommand.
/// </summary>
public sealed class DeactivateDeviceTokenCommandHandler : IRequestHandler<DeactivateDeviceTokenCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="DeactivateDeviceTokenCommandHandler"/>.
    /// </summary>
    public DeactivateDeviceTokenCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(DeactivateDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var normalizedToken = request.Token.Trim();
        var existing = await _dbContext.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == normalizedToken && t.UserId == callerUserId, cancellationToken);

        if (existing != null && existing.IsActive)
        {
            existing.Deactivate(DateTime.UtcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
