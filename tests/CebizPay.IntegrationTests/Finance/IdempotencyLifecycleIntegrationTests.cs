using CebizPay.Application.Common.Exceptions;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

public sealed class IdempotencyLifecycleIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public IdempotencyLifecycleIntegrationTests(InfrastructureFixture fixture)
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
    public async Task CreateRecordAsync_NormalCompletionAndReplay_ShouldPreserveCompletedResponse()
    {
        // Arrange
        await using var db = CreateDbContext();
        var service = new IdempotencyService(db);
        var key = Guid.NewGuid().ToString();
        var userId = $"user_{Guid.NewGuid():N}";
        var payload = "{\"amount\": 500, \"currency\": \"NGN\"}";

        // Act 1: Initial record creation
        var record = await service.CreateRecordAsync(key, "Wallet.PeerTransfer", payload, userId: userId);
        Assert.Equal(IdempotencyStatus.Processing, record.Status);

        // Complete record
        var responseJson = "{\"status\": \"COMPLETED\", \"reference\": \"CBZPT-12345\"}";
        await service.CompleteRecordAsync(record.Id, responseJson);

        // Act 2: Replay same request
        var replayRecord = await service.CreateRecordAsync(key, "Wallet.PeerTransfer", payload, userId: userId);

        // Assert
        Assert.Equal(IdempotencyStatus.Completed, replayRecord.Status);
        Assert.Equal(responseJson, replayRecord.ResponseJson);
    }

    [Fact]
    public async Task CreateRecordAsync_PayloadMismatch_ShouldThrowIdempotencyConflictException()
    {
        // Arrange
        await using var db = CreateDbContext();
        var service = new IdempotencyService(db);
        var key = Guid.NewGuid().ToString();
        var userId = $"user_{Guid.NewGuid():N}";

        await service.CreateRecordAsync(key, "Wallet.PeerTransfer", "{\"amount\": 500}", userId: userId);

        // Act & Assert - Attempt with same key but different payload
        var ex = await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            service.CreateRecordAsync(key, "Wallet.PeerTransfer", "{\"amount\": 1000}", userId: userId));

        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", ex.Code);
        Assert.Contains("different request payload", ex.Message);
    }

    [Fact]
    public async Task CreateRecordAsync_StaleProcessingRecord_RecoversForCleanRetry()
    {
        // Arrange - Insert an in-flight record created 5 minutes ago (simulating crashed mid-flight operation)
        await using var db = CreateDbContext();
        var service = new IdempotencyService(db);
        var key = Guid.NewGuid().ToString();
        var userId = $"user_{Guid.NewGuid():N}";
        var payload = "{\"amount\": 750, \"currency\": \"NGN\"}";

        var staleRecord = new IdempotencyRecord(key, "Wallet.PeerTransfer",
            CebizPay.Application.Common.Security.HashUtility.ComputeSha256(payload), userId);

        // Force CreatedAtUtc back by 5 minutes
        typeof(IdempotencyRecord).GetProperty("CreatedAtUtc")!
            .SetValue(staleRecord, DateTime.UtcNow.AddMinutes(-5));

        db.IdempotencyRecords.Add(staleRecord);
        await db.SaveChangesAsync();

        // Act - Client retries the operation after timeout
        var recovered = await service.CreateRecordAsync(key, "Wallet.PeerTransfer", payload, userId: userId);

        // Assert - Recovered record is in Processing status and ready for execution
        Assert.Equal(IdempotencyStatus.Processing, recovered.Status);
        Assert.Null(recovered.ResponseJson);
    }

    [Fact]
    public async Task CreateRecordAsync_FailedRecord_AllowsCleanRetry()
    {
        // Arrange
        await using var db = CreateDbContext();
        var service = new IdempotencyService(db);
        var key = Guid.NewGuid().ToString();
        var userId = $"user_{Guid.NewGuid():N}";
        var payload = "{\"amount\": 250, \"currency\": \"NGN\"}";

        var record = await service.CreateRecordAsync(key, "Wallet.PeerTransfer", payload, userId: userId);
        await service.FailRecordAsync(record.Id, "Temporary timeout");

        // Act - Retry failed operation with exact same key and payload
        var retried = await service.CreateRecordAsync(key, "Wallet.PeerTransfer", payload, userId: userId);

        // Assert
        Assert.Equal(IdempotencyStatus.Processing, retried.Status);
    }
}
