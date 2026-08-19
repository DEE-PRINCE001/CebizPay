using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using CebizPay.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CebizPay.UnitTests.Messaging;

public sealed class OutboxDeadLetteringWorkerTests
{
    private static ServiceProvider CreateServiceProvider(string dbName, IEventPublisher eventPublisher)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: dbName)
                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));
        services.AddScoped(_ => eventPublisher);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ProcessOutboxMessages_WhenPublishFails_IncrementsRetryCountAndLogsError()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher.PublishAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("RabbitMQ connection lost."));

        var provider = CreateServiceProvider(dbName, eventPublisher);

        var messageId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = messageId,
            Type = "TestEvent",
            Content = "{\"test\": 1}",
            OccurredOnUtc = DateTime.UtcNow,
            RetryCount = 0
        };

        using (var setupScope = provider.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OutboxMessages.Add(message);
            await db.SaveChangesAsync();
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);

        // Act
        var processMethod = typeof(OutboxPublisherWorker).GetMethod("ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var processedCount = await (Task<int>)processMethod.Invoke(worker, new object[] { CancellationToken.None })!;

        // Assert
        Assert.Equal(1, processedCount);

        using (var verifyScope = provider.CreateScope())
        {
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var savedMessage = await verifyDb.OutboxMessages.FirstAsync(m => m.Id == messageId);
            Assert.Equal(1, savedMessage.RetryCount);
            Assert.Null(savedMessage.ProcessedOnUtc);
            Assert.Null(savedMessage.DeadLetteredOnUtc);
            Assert.NotNull(savedMessage.LastAttemptedOnUtc);
            Assert.Equal("RabbitMQ connection lost.", savedMessage.Error);
        }
    }

    [Fact]
    public async Task ProcessOutboxMessages_WhenRetriesExhausted_MarksDeadLettered_AndAllowsSubsequentMessagesToProcess()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var poisonId = Guid.NewGuid();
        var validId = Guid.NewGuid();

        var poisonMessage = new OutboxMessage
        {
            Id = poisonId,
            Type = "PoisonEvent",
            Content = "{\"corrupt\": true}",
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-5),
            RetryCount = 4 // 5th attempt will exhaust retries
        };

        var validMessage = new OutboxMessage
        {
            Id = validId,
            Type = "ValidEvent",
            Content = "{\"valid\": true}",
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-1),
            RetryCount = 0
        };

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher.PublishAsync(poisonMessage.Content, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Unroutable message format"));
        eventPublisher.PublishAsync(validMessage.Content, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var provider = CreateServiceProvider(dbName, eventPublisher);

        using (var setupScope = provider.CreateScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.OutboxMessages.AddRange(poisonMessage, validMessage);
            await db.SaveChangesAsync();
        }

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var worker = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);

        var processMethod = typeof(OutboxPublisherWorker).GetMethod("ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        // Act 1: Process batch containing poison and valid message
        await (Task<int>)processMethod.Invoke(worker, new object[] { CancellationToken.None })!;

        // Assert 1: Poison message reached 5 retries and is dead-lettered
        using (var verifyScope1 = provider.CreateScope())
        {
            var verifyDb1 = verifyScope1.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var poisonInDb = await verifyDb1.OutboxMessages.FirstAsync(m => m.Id == poisonId);
            var validInDb = await verifyDb1.OutboxMessages.FirstAsync(m => m.Id == validId);

            Assert.Equal(5, poisonInDb.RetryCount);
            Assert.NotNull(poisonInDb.DeadLetteredOnUtc);
            Assert.Null(poisonInDb.ProcessedOnUtc);

            Assert.NotNull(validInDb.ProcessedOnUtc);
            Assert.Null(validInDb.Error);
        }

        // Act 2: Add subsequent message and verify dead-lettered message is skipped
        var nextValidId = Guid.NewGuid();
        var newValidMessage = new OutboxMessage
        {
            Id = nextValidId,
            Type = "NextValidEvent",
            Content = "{\"next\": true}",
            OccurredOnUtc = DateTime.UtcNow,
            RetryCount = 0
        };

        using (var addScope = provider.CreateScope())
        {
            var addDb = addScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            addDb.OutboxMessages.Add(newValidMessage);
            await addDb.SaveChangesAsync();
        }

        eventPublisher.PublishAsync(newValidMessage.Content, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var processedCount = await (Task<int>)processMethod.Invoke(worker, new object[] { CancellationToken.None })!;

        // Assert 2: Only 1 new message processed, dead-lettered message was not picked up
        Assert.Equal(1, processedCount);

        using (var verifyScope2 = provider.CreateScope())
        {
            var verifyDb2 = verifyScope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var nextInDb = await verifyDb2.OutboxMessages.FirstAsync(m => m.Id == nextValidId);
            Assert.NotNull(nextInDb.ProcessedOnUtc);
        }
    }
}
