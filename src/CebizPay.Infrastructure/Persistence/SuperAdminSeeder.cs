using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Identity;
using CebizPay.Infrastructure.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable CA1848, CA1873

namespace CebizPay.Infrastructure.Persistence;

/// <summary>
/// Seeder responsible for ensuring an initial Super Admin account is provisioned upon startup.
/// </summary>
public static class SuperAdminSeeder
{
    /// <summary>
    /// Seeds the Super Admin user and AdminProfile if not already present.
    /// </summary>
    public static async Task SeedSuperAdminAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        IOptions<SuperAdminSeedOptions> options,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.Email) || string.IsNullOrWhiteSpace(config.Password))
        {
            return;
        }

        // Check if any Super Admin profile already exists
        var hasSuperAdmin = await dbContext.AdminProfiles
            .AnyAsync(a => a.Role == AdminRoleType.SuperAdmin && a.IsActive, cancellationToken);

        if (hasSuperAdmin)
        {
            return;
        }

        logger.LogInformation("No active Super Admin found in database. Seeding Super Admin ({Email})...", config.Email);

        var user = await userManager.FindByEmailAsync(config.Email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = config.Email,
                Email = config.Email,
                PhoneNumber = string.IsNullOrWhiteSpace(config.PhoneNumber) ? "+2348000000000" : config.PhoneNumber,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, config.Password);
            if (!createResult.Succeeded)
            {
                logger.LogError("Failed to seed Super Admin user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                user.PasswordHistoryJson = JsonSerializer.Serialize(new List<string> { user.PasswordHash });
                await userManager.UpdateAsync(user);
            }
        }

        // Ensure IndividualProfile exists
        var individualProfile = await dbContext.IndividualProfiles
            .FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (individualProfile == null)
        {
            individualProfile = new IndividualProfile(user.Id, config.FirstName, config.LastName);
            dbContext.IndividualProfiles.Add(individualProfile);
        }

        // Ensure AdminProfile with SuperAdmin role exists
        var adminProfile = await dbContext.AdminProfiles
            .FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);
        if (adminProfile == null)
        {
            adminProfile = new AdminProfile(user.Id, AdminRoleType.SuperAdmin, isMfaEnabled: false);
            dbContext.AdminProfiles.Add(adminProfile);
        }
        else if (adminProfile.Role != AdminRoleType.SuperAdmin || !adminProfile.IsActive)
        {
            adminProfile.ChangeRole(AdminRoleType.SuperAdmin);
            adminProfile.Activate();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Super Admin successfully seeded with UserId: {UserId} ({Email})", user.Id, config.Email);
    }
}
