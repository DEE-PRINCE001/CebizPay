using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CebizPay.Application;

/// <summary>
/// Service collection extensions for configuring Application layer dependencies.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds Application layer services (MediatR, FluentValidation) to the service collection.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(AssemblyReference).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
