using CebizPay.Application.Common.Extensions;
using CebizPay.Application.Common.Interfaces.Persistence;
using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Application.Common.Interfaces.Storage;
using CebizPay.Domain.Enums;
using FluentValidation;
using MediatR;

namespace CebizPay.Application.UseCases.Organizations.Kyb;

/// <summary>
/// Command to upload a KYB document securely to Cloudinary storage.
/// </summary>
public sealed record UploadKybDocumentCommand(
    Guid OrganizationId,
    string DocumentType,
    Stream FileStream,
    string FileName,
    string ContentType,
    long FileSizeBytes) : IRequest<KybDocumentUploadResponseDto>;

/// <summary>
/// Response DTO containing verified storage upload metadata.
/// </summary>
public sealed record KybDocumentUploadResponseDto(
    Guid OrganizationId,
    string DocumentType,
    string FileUrl,
    string PublicId,
    long FileSizeBytes,
    DateTime UploadedAtUtc);

/// <summary>
/// Validator for UploadKybDocumentCommand.
/// </summary>
public sealed class UploadKybDocumentCommandValidator : AbstractValidator<UploadKybDocumentCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Initializes validation rules for UploadKybDocumentCommand.
    /// </summary>
    public UploadKybDocumentCommandValidator()
    {
        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .WithMessage("OrganizationId is required.");

        RuleFor(x => x.DocumentType)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("DocumentType is required.");

        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("FileName is required.");

        RuleFor(x => x.ContentType)
            .NotEmpty()
            .WithMessage("ContentType is required.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File cannot be empty.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage("File size exceeds the 10MB limit.");

        RuleFor(x => x.FileStream)
            .NotNull()
            .WithMessage("File stream is required.");
    }
}

/// <summary>
/// Handler for UploadKybDocumentCommand.
/// </summary>
public sealed class UploadKybDocumentCommandHandler : IRequestHandler<UploadKybDocumentCommand, KybDocumentUploadResponseDto>
{
    private readonly ICloudinaryStorageService _storageService;
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Initializes a new instance of <see cref="UploadKybDocumentCommandHandler"/>.
    /// </summary>
    public UploadKybDocumentCommandHandler(
        ICloudinaryStorageService storageService,
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc/>
    public async Task<KybDocumentUploadResponseDto> Handle(UploadKybDocumentCommand request, CancellationToken cancellationToken)
    {
        var callerUserId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(callerUserId))
            throw new UnauthorizedAccessException("Authentication is required to upload organization documents.");

        // Enforce Tenant Access: verify caller has active membership in target organization
        var membership = await _dbContext.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.OrganizationId == request.OrganizationId && m.UserId == callerUserId && m.Status == MembershipStatus.Active, cancellationToken);

        if (membership == null)
            throw new UnauthorizedAccessException($"Access to organization {request.OrganizationId} is denied.");

        var org = await _dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Organization with ID {request.OrganizationId} not found.");

        var folder = $"cebizpay/kyb/{request.OrganizationId:N}";
        var uploadResult = await _storageService.UploadAsync(
            request.FileStream,
            request.FileName,
            request.ContentType,
            folder,
            cancellationToken);

        if (!uploadResult.Succeeded || string.IsNullOrWhiteSpace(uploadResult.SecureUrl))
        {
            throw new InvalidOperationException(uploadResult.ErrorMessage ?? "Failed to upload document to storage provider.");
        }

        var normalizedType = request.DocumentType.Trim();

        // Update organization aggregate if standard document type
        if (string.Equals(normalizedType, "CacCertificate", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedType, "Certificate", StringComparison.OrdinalIgnoreCase))
        {
            org.SetCacCertificateUrl(uploadResult.SecureUrl);
        }
        else if (string.Equals(normalizedType, "Logo", StringComparison.OrdinalIgnoreCase))
        {
            org.SetLogoUrl(uploadResult.SecureUrl);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new KybDocumentUploadResponseDto(
            OrganizationId: request.OrganizationId,
            DocumentType: normalizedType,
            FileUrl: uploadResult.SecureUrl,
            PublicId: uploadResult.PublicId ?? string.Empty,
            FileSizeBytes: uploadResult.Bytes > 0 ? uploadResult.Bytes : request.FileSizeBytes,
            UploadedAtUtc: DateTime.UtcNow);
    }
}
