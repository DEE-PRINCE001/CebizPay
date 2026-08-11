using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Represents the database context for the application, including ASP.NET Core Identity data.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for configuring the database context.</param>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
}