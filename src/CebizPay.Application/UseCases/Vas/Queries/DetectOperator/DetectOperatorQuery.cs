using CebizPay.Application.Common.Models.Vas;
using MediatR;

namespace CebizPay.Application.UseCases.Vas.Queries.DetectOperator;

/// <summary>
/// Query to automatically resolve the network operator for a recipient phone number.
/// </summary>
public sealed record DetectOperatorQuery(string PhoneNumber) : IRequest<OperatorDetectionResponseDto>;
