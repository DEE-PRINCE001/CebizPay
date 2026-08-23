using CebizPay.Application.Common.Interfaces.Vas;
using CebizPay.Application.Common.Models.Vas;
using CebizPay.Application.Common.Utils;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.DetectOperator;

/// <summary>
/// Handles <see cref="DetectOperatorQuery"/>.
/// Uses <see cref="IVasProvider.ResolveOperatorAsync"/> with fallback to prefix-based detection.
/// </summary>
public sealed class DetectOperatorQueryHandler : IRequestHandler<DetectOperatorQuery, OperatorDetectionResponseDto>
{
    private readonly IVasProvider _vasProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="DetectOperatorQueryHandler"/>.
    /// </summary>
    public DetectOperatorQueryHandler(IVasProvider vasProvider)
    {
        _vasProvider = vasProvider;
    }

    /// <inheritdoc/>
    public async Task<OperatorDetectionResponseDto> Handle(DetectOperatorQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            return new OperatorDetectionResponseDto(false, null, "Phone number is required.");
        }

        var normalized = PhoneNormalizer.NormalizeNational(request.PhoneNumber);
        if (!PhoneNormalizer.IsValidNigerianPhoneNumber(normalized))
        {
            return new OperatorDetectionResponseDto(false, null, "Invalid Nigerian phone number.");
        }

        // Try provider detection first
        var providerResult = await _vasProvider.ResolveOperatorAsync(normalized, cancellationToken);
        if (providerResult.Succeeded && providerResult.Network.HasValue)
        {
            return new OperatorDetectionResponseDto(true, providerResult.Network.Value.ToString().ToUpperInvariant(), null);
        }

        // Fallback to prefix-based lookup
        var prefixDetected = PhoneNormalizer.DetectNetworkFromPrefix(normalized);
        if (prefixDetected.HasValue)
        {
            return new OperatorDetectionResponseDto(true, prefixDetected.Value.ToString().ToUpperInvariant(), null);
        }

        return new OperatorDetectionResponseDto(false, null, "Unable to resolve operator for the specified phone number.");
    }
}
