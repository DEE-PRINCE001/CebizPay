using System.Text;
using CebizPay.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace CebizPay.Infrastructure.Security;

/// <summary>
/// Service collection extensions for configuring authentication and authorization.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Adds JWT authentication and basic authorization policies.
    /// </summary>
    public static IServiceCollection AddJwtSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration section is missing.");

        var key = Encoding.UTF8.GetBytes(jwtOptions.Secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Set to true in prod via reverse proxy if HTTPS terminates at edge
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = TimeSpan.FromSeconds(5)
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireSuperAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CebizPay.Domain.Enums.AdminRoleType.SuperAdmin.ToString()))
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireComplianceAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CebizPay.Domain.Enums.AdminRoleType.SuperAdmin.ToString(), CebizPay.Domain.Enums.AdminRoleType.Admin.ToString()))
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireFinanceAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CebizPay.Domain.Enums.AdminRoleType.SuperAdmin.ToString(), CebizPay.Domain.Enums.AdminRoleType.Admin.ToString()))
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePlatformAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CebizPay.Domain.Enums.AdminRoleType.SuperAdmin.ToString(), CebizPay.Domain.Enums.AdminRoleType.Admin.ToString()))
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireAuditor, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CebizPay.Domain.Enums.AdminRoleType.SuperAdmin.ToString(), CebizPay.Domain.Enums.AdminRoleType.Admin.ToString(), CebizPay.Domain.Enums.AdminRoleType.Auditor.ToString()))
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireOrganizationFinanceApproval, policy =>
                policy.RequireAuthenticatedUser())
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequirePayrollExecution, policy =>
                policy.RequireAuthenticatedUser())
            .AddPolicy(CebizPay.Application.Common.Security.AuthorizationPolicies.RequireWorkforceManagement, policy =>
                policy.RequireAuthenticatedUser());

        return services;
    }
}
