using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Domain.Communication.Entities;
using CebizPay.Domain.Communication.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Notifications;

/// <summary>
/// Command to register or reactivate an FCM device token for push notifications.
/// </summary>
public sealed record RegisterDeviceTokenCommand(
    string Token,
    DevicePlatform Platform,
    string? DeviceModel = null) : IRequest<bool>;

/// <summary>
/// Validator for RegisterDeviceTokenCommand.
/// </summary>
public sealed class RegisterDeviceTokenCommandValidator : AbstractValidator<RegisterDeviceTokenCommand>
{
    /// <summary>
    /// Initializes validation rules for RegisterDeviceTokenCommand.
    /// </summary>
    public RegisterDeviceTokenCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Device token is required.")
            .MaximumLength(500).WithMessage("Device token cannot exceed 500 characters.");

        RuleFor(x => x.Platform)
            .IsInEnum().WithMessage("A valid device platform (Android, iOS, Web) must be specified.");

        RuleFor(x => x.DeviceModel)
            .MaximumLength(150).WithMessage("DeviceModel cannot exceed 150 characters.");
    }
}

/// <summary>
/// Handler for RegisterDeviceTokenCommand.
/// </summary>
public sealed class RegisterDeviceTokenCommandHandler : IRequestHandler<RegisterDeviceTokenCommand, bool>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="RegisterDeviceTokenCommandHandler"/>.
    /// </summary>
    public RegisterDeviceTokenCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<bool> Handle(RegisterDeviceTokenCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
        {
            throw new UnauthorizedAccessException("Authentication required.");
        }

        var normalizedToken = request.Token.Trim();
        var existing = await _dbContext.DeviceTokens
            .FirstOrDefaultAsync(t => t.Token == normalizedToken, cancellationToken);

        var now = DateTime.UtcNow;

        if (existing != null)
        {
            existing.Activate(callerUserId, now, request.DeviceModel);
        }
        else
        {
            var deviceToken = DeviceToken.Create(
                callerUserId,
                normalizedToken,
                request.Platform,
                request.DeviceModel);

            _dbContext.DeviceTokens.Add(deviceToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
