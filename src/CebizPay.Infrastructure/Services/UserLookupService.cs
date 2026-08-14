using CebizPay.Application.Common.Interfaces.Security;
using CebizPay.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of IUserLookupService using ASP.NET Core Identity UserManager.
/// </summary>
public sealed class UserLookupService : IUserLookupService
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes a new instance of <see cref="UserLookupService"/>.
    /// </summary>
    public UserLookupService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <inheritdoc/>
    public async Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user == null ? null : new UserSummary(user.Id, user.Email, user.PhoneNumber);
    }

    /// <inheritdoc/>
    public async Task<UserSummary?> FindByPhoneAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, cancellationToken);
        return user == null ? null : new UserSummary(user.Id, user.Email, user.PhoneNumber);
    }
}
