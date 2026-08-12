using CebizPay.Application.Common.Interfaces.Caching;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Caching;
using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CebizPay.Infrastructure;

/// <summary>
/// Service collection extensions for configuring Infrastructure dependencies.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind and validate options
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RedisOptions>()
            .Bind(configuration.GetSection(RedisOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RabbitMQOptions>()
            .Bind(configuration.GetSection(RabbitMQOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        // Configure PostgreSQL & EF Core
        var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? dbOptions.DefaultConnection;

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.CommandTimeout(dbOptions.CommandTimeoutInSeconds);
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: dbOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        // Configure Identity
        services.AddIdentityCore<IdentityUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Configure Redis
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();
        var redisConnString = configuration.GetConnectionString("Redis")
            ?? redisOptions?.ConnectionString
            ?? "localhost:6379";

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnString));
        services.AddSingleton<ICacheService, RedisCacheService>();

        // Configure RabbitMQ
        services.AddTransient<IEventPublisher, RabbitMQEventPublisher>();

        // Configure Health Checks
        var rabbitOptions = configuration.GetSection(RabbitMQOptions.SectionName).Get<RabbitMQOptions>();
        var rabbitConnString = configuration.GetConnectionString("RabbitMQ")
            ?? $"amqp://{rabbitOptions?.UserName ?? "cebizpay"}:{rabbitOptions?.Password ?? "cebizpay_dev"}@{rabbitOptions?.HostName ?? "localhost"}:{rabbitOptions?.Port ?? 5672}";

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql")
            .AddRedis(redisConnString, name: "redis")
            .AddRabbitMQ(
                async _ =>
                {
                    var factory = new RabbitMQ.Client.ConnectionFactory { Uri = new Uri(rabbitConnString) };
                    return await factory.CreateConnectionAsync().ConfigureAwait(false);
                },
                name: "rabbitmq");

        return services;
    }
}
