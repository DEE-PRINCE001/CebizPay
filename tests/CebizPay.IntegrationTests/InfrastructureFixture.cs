using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace CebizPay.IntegrationTests;

public sealed class InfrastructureFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgresContainer { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:17")
        .WithDatabase("cebizpay_test")
        .WithUsername("cebizpay")
        .WithPassword("cebizpay_test_pass")
        .Build();

    public RedisContainer RedisContainer { get; } = new RedisBuilder()
        .WithImage("redis:8-alpine")
        .Build();

    public RabbitMqContainer RabbitMqContainer { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management-alpine")
        .WithUsername("cebizpay")
        .WithPassword("cebizpay_test_pass")
        .Build();

    public async Task InitializeAsync()
    {
        await PostgresContainer.StartAsync().ConfigureAwait(false);
        await RedisContainer.StartAsync().ConfigureAwait(false);
        await RabbitMqContainer.StartAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await PostgresContainer.DisposeAsync().ConfigureAwait(false);
        await RedisContainer.DisposeAsync().ConfigureAwait(false);
        await RabbitMqContainer.DisposeAsync().ConfigureAwait(false);
    }
}
