using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using RabbitMQ.Client;
using Xunit;

namespace CebizPay.UnitTests.Messaging;

public sealed class RabbitMqConnectionLifecycleTests
{
    [Fact]
    public async Task PublishAsync_MultipleCalls_ReusesPersistentConnection()
    {
        // Arrange
        var mockConnectionProvider = Substitute.For<IRabbitMqConnectionProvider>();
        var mockConnection = Substitute.For<IConnection>();
        var mockChannel = Substitute.For<IChannel>();

        mockConnection.IsOpen.Returns(true);
        mockConnectionProvider.GetConnectionAsync(Arg.Any<CancellationToken>()).Returns(mockConnection);
        mockConnection.CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>())
            .Returns(mockChannel);

        var options = Options.Create(new RabbitMQOptions
        {
            ExchangeName = "test.events"
        });

        var publisher = new RabbitMQEventPublisher(
            mockConnectionProvider,
            options,
            NullLogger<RabbitMQEventPublisher>.Instance);

        var testEvent1 = new { Id = Guid.NewGuid(), Name = "Event1" };
        var testEvent2 = new { Id = Guid.NewGuid(), Name = "Event2" };

        // Act
        await publisher.PublishAsync(testEvent1);
        await publisher.PublishAsync(testEvent2);

        // Assert - Connection was requested twice from provider (which returns same persistent connection),
        // and connection itself was NOT closed or disposed.
        await mockConnectionProvider.Received(2).GetConnectionAsync(Arg.Any<CancellationToken>());
        await mockConnection.Received(2).CreateChannelAsync(Arg.Any<CreateChannelOptions>(), Arg.Any<CancellationToken>());
        await mockConnection.DidNotReceive().CloseAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RabbitMqConnectionProvider_WhenDisposed_DisposesUnderlyingConnection()
    {
        // Arrange
        var options = Options.Create(new RabbitMQOptions
        {
            HostName = "invalid-host-to-avoid-actual-connection",
            Port = 5672,
            Password = "pass"
        });

        var provider = new RabbitMqConnectionProvider(options, NullLogger<RabbitMqConnectionProvider>.Instance);

        // Act & Assert
        provider.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => provider.GetConnectionAsync());
    }
}
