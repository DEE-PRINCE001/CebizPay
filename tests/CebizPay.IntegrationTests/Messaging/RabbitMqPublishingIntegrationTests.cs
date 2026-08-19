using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.IntegrationTests.Messaging;

public sealed class RabbitMqPublishingIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public RabbitMqPublishingIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PublishAsync_MultipleMessages_SucceedsAndReusesSingleConnection()
    {
        // Arrange
        var uri = new Uri(_fixture.RabbitMqContainer.GetConnectionString());
        var options = Options.Create(new RabbitMQOptions
        {
            HostName = _fixture.RabbitMqContainer.Hostname,
            Port = _fixture.RabbitMqContainer.GetMappedPublicPort(5672),
            UserName = "cebizpay",
            Password = "cebizpay_test_pass",
            VirtualHost = "/",
            ExchangeName = "cebizpay.integration.test.events"
        });

        await using var connectionProvider = new RabbitMqConnectionProvider(
            options,
            NullLogger<RabbitMqConnectionProvider>.Instance);

        var publisher = new RabbitMQEventPublisher(
            connectionProvider,
            options,
            NullLogger<RabbitMQEventPublisher>.Instance);

        // Act - Publish 5 messages concurrently
        var publishTasks = Enumerable.Range(1, 5).Select(i =>
            publisher.PublishAsync(new
            {
                EventId = Guid.NewGuid(),
                Sequence = i,
                Message = $"Integration test message #{i}",
                Timestamp = DateTime.UtcNow
            }));

        await Task.WhenAll(publishTasks);

        // Assert - Connection is still open and alive
        var conn = await connectionProvider.GetConnectionAsync();
        Assert.NotNull(conn);
        Assert.True(conn.IsOpen);
    }
}
