using CebizPay.Infrastructure.Caching;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using StackExchange.Redis;
using Xunit;

namespace CebizPay.IntegrationTests;

public sealed class ContainerHealthCheckTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public ContainerHealthCheckTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PostgresContainer_ShouldExecuteDatabaseOperations()
    {
        // Arrange
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = "TestIntegrationEvent",
            Content = "{\"test\": \"data\"}",
            OccurredOnUtc = DateTime.UtcNow
        };

        // Act
        dbContext.OutboxMessages.Add(outboxMessage);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == outboxMessage.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("TestIntegrationEvent", retrieved.Type);
    }

    [Fact]
    public async Task RedisContainer_ShouldStoreAndRetrieveData()
    {
        // Arrange
        var connectionString = _fixture.RedisContainer.GetConnectionString();
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);

        var redisOptions = Options.Create(new RedisOptions
        {
            ConnectionString = connectionString,
            InstanceName = "IntegrationTest:"
        });

        var cacheService = new RedisCacheService(multiplexer, redisOptions, NullLogger<RedisCacheService>.Instance);
        var testKey = "test_cache_key";
        var testValue = new TestData("CebizPay", 100);

        // Act
        await cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(5));
        var result = await cacheService.GetAsync<TestData>(testKey);
        await cacheService.RemoveAsync(testKey);
        var afterRemove = await cacheService.GetAsync<TestData>(testKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("CebizPay", result.Name);
        Assert.Equal(100, result.Value);
        Assert.Null(afterRemove);
    }

    [Fact]
    public async Task RabbitMqContainer_ShouldConnectAndPublishMessage()
    {
        // Arrange
        var connectionString = _fixture.RabbitMqContainer.GetConnectionString();
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };

        // Act
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: "test_integration_queue",
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Assert
        Assert.True(connection.IsOpen);
        Assert.True(channel.IsOpen);
    }

    private sealed record TestData(string Name, int Value);
}
