using MediatR;

namespace CebizPay.Application.UseCases.Auth.GetCurrentUser;

/// <summary>
/// Query to retrieve the full profile and context of the currently authenticated user.
/// </summary>
public sealed record GetCurrentUserQuery : IRequest<CurrentUserDto>;
