using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using CebizPay.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace CebizPay.IntegrationTests.Messaging;

public sealed class OutboxDeadLetterIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public OutboxDeadLetterIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    [Fact]
    public async Task OutboxPublisherWorker_InPostgreSql_DeadLettersPoisonMessage_AndProcessesSubsequentValidMessages()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var poisonId = Guid.NewGuid();
        var validId = Guid.NewGuid();

        var poisonMessage = new OutboxMessage
        {
            Id = poisonId,
            Type = "PoisonIntegrationEvent",
            Content = "{\"poison\": true}",
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-10),
            RetryCount = 4
        };

        var validMessage = new OutboxMessage
        {
            Id = validId,
            Type = "ValidIntegrationEvent",
            Content = "{\"valid\": true}",
            OccurredOnUtc = DateTime.UtcNow.AddMinutes(-5),
            RetryCount = 0
        };

        dbContext.OutboxMessages.AddRange(poisonMessage, validMessage);
        await dbContext.SaveChangesAsync();

        var eventPublisher = Substitute.For<IEventPublisher>();
        eventPublisher.PublishAsync(poisonMessage.Content, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Fatal serialization defect in event"));
        eventPublisher.PublishAsync(validMessage.Content, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => CreateDbContext());
        services.AddScoped(_ => eventPublisher);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var worker = new OutboxPublisherWorker(scopeFactory, NullLogger<OutboxPublisherWorker>.Instance);

        var processMethod = typeof(OutboxPublisherWorker).GetMethod("ProcessOutboxMessagesAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        // Act 1 - Process batch
        await (Task<int>)processMethod.Invoke(worker, new object[] { CancellationToken.None })!;

        // Assert 1 - Check database state
        await using var verifyDb1 = CreateDbContext();
        var poisonDb1 = await verifyDb1.OutboxMessages.FirstAsync(m => m.Id == poisonId);
        var validDb1 = await verifyDb1.OutboxMessages.FirstAsync(m => m.Id == validId);

        Assert.Equal(5, poisonDb1.RetryCount);
        Assert.NotNull(poisonDb1.DeadLetteredOnUtc);
        Assert.Null(poisonDb1.ProcessedOnUtc);
        Assert.NotNull(poisonDb1.LastAttemptedOnUtc);
        Assert.Contains("Fatal serialization defect", poisonDb1.Error);

        Assert.NotNull(validDb1.ProcessedOnUtc);
        Assert.Null(validDb1.DeadLetteredOnUtc);
        Assert.Null(validDb1.Error);

        // Act 2 - Add another valid message and process next cycle
        var nextValidId = Guid.NewGuid();
        var nextValidMessage = new OutboxMessage
        {
            Id = nextValidId,
            Type = "NextValidEvent",
            Content = "{\"next\": 123}",
            OccurredOnUtc = DateTime.UtcNow,
            RetryCount = 0
        };
        verifyDb1.OutboxMessages.Add(nextValidMessage);
        await verifyDb1.SaveChangesAsync();

        eventPublisher.PublishAsync(nextValidMessage.Content, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var count = await (Task<int>)processMethod.Invoke(worker, new object[] { CancellationToken.None })!;

        // Assert 2 - Dead-lettered message was excluded by SQL query (SKIP LOCKED / dead-letter filter)
        Assert.Equal(1, count);

        await using var verifyDb2 = CreateDbContext();
        var nextValidDb = await verifyDb2.OutboxMessages.FirstAsync(m => m.Id == nextValidId);
        Assert.NotNull(nextValidDb.ProcessedOnUtc);
    }
}
