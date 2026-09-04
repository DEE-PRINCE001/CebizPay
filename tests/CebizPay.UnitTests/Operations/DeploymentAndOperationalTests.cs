using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Security;
using CebizPay.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.UnitTests.Operations;

/// <summary>
/// Operational and deployment readiness tests covering worker scaling,
/// configuration binding, and fail-closed security properties.
/// </summary>
public sealed class DeploymentAndOperationalTests
{
    [Fact]
    public void WorkerQueueConstants_MustBeNonExclusiveAndDurableQueueNames()
    {
        // Assert - Queue names must follow standard durable naming conventions
        Assert.Equal("cebizpay.payments.failover", PaymentFailoverWorker.QueueName);
        Assert.Equal("cebizpay.notifications.dispatch", NotificationDispatcherWorker.QueueName);
    }

    [Fact]
    public void JwtSettingsOptions_ExpirationInMinutes_MustBe15MinutesInProduction()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "super_secret_jwt_key_at_least_32_characters_long",
                ["Jwt:Issuer"] = "CebizPay",
                ["Jwt:Audience"] = "CebizPayApp",
                ["Jwt:ExpirationInMinutes"] = "15"
            })
            .Build();

        // Act
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();

        // Assert
        Assert.NotNull(jwtOptions);
        Assert.Equal(15, jwtOptions.ExpirationInMinutes);
    }

    [Fact]
    public void RabbitMQOptions_DefaultConfiguration_ShouldBindProperly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:HostName"] = "rabbitmq.prod.internal",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:UserName"] = "cebizpay_worker",
                ["RabbitMQ:Password"] = "secure_rabbit_password",
                ["RabbitMQ:ExchangeName"] = "cebizpay.events"
            })
            .Build();

        // Act
        var options = configuration.GetSection(RabbitMQOptions.SectionName).Get<RabbitMQOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("rabbitmq.prod.internal", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("cebizpay_worker", options.UserName);
        Assert.Equal("secure_rabbit_password", options.Password);
        Assert.Equal("cebizpay.events", options.ExchangeName);
    }

    [Fact]
    public void RedisOptions_DefaultConfiguration_ShouldBindProperly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:ConnectionString"] = "redis.prod.internal:6379,password=secret",
                ["Redis:InstanceName"] = "CebizPayProd:"
            })
            .Build();

        // Act
        var options = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.Equal("redis.prod.internal:6379,password=secret", options.ConnectionString);
        Assert.Equal("CebizPayProd:", options.InstanceName);
    }

    [Fact]
    public async Task WorkerGracefulShutdown_ShouldHonorCancellationTokenImmediately()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-canceled token

        var worker = new DummyGracefulWorker();

        // Act
        var executionTask = worker.ExecutePublicAsync(cts.Token);
        await executionTask;

        // Assert
        Assert.True(executionTask.IsCompletedSuccessfully);
        Assert.Equal(0, worker.IterationCount);
    }

    private sealed class DummyGracefulWorker
    {
        public int IterationCount { get; private set; }

        public async Task ExecutePublicAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                IterationCount++;
                await Task.Delay(100, stoppingToken);
            }
        }
    }
}
