using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for <see cref="PaymentAttempt"/> persistence, unique constraints, and schema mappings.
/// </summary>
public sealed class PaymentAttemptIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PaymentAttemptIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task PaymentAttempt_Persistence_ShouldSaveAndRetrieveCorrectly()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();
        var requestRef = $"CBZPA-{Guid.NewGuid():N}";

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: requestRef,
            amount: 12500.50m,
            currency: Currency.NGN,
            safeMetadata: "{\"provider_route\":\"direct_bank\"}");

        // Act
        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var retrieved = await readContext.PaymentAttempts.FirstOrDefaultAsync(p => p.Id == attempt.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(ledgerTxId, retrieved.LedgerTransactionId);
        Assert.Equal(PaymentProvider.Flutterwave, retrieved.Provider);
        Assert.Equal(1, retrieved.AttemptNumber);
        Assert.Equal(PaymentAttemptStatus.Created, retrieved.Status);
        Assert.Equal(requestRef, retrieved.RequestReference);
        Assert.Equal(12500.50m, retrieved.Amount);
        Assert.Equal(Currency.NGN, retrieved.Currency);
        Assert.Equal("{\"provider_route\":\"direct_bank\"}", retrieved.SafeMetadata);
        Assert.Null(retrieved.ProviderReference);
        Assert.Null(retrieved.StartedAtUtc);
        Assert.Null(retrieved.CompletedAtUtc);
    }

    [Fact]
    public async Task PaymentAttempt_UniqueConstraint_TransactionAndAttemptNumber_ShouldThrowOnDuplicate()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 5000m,
            currency: Currency.NGN);

        var attempt2 = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Paystack,
            attemptNumber: 1, // Duplicate attempt number for same ledger transaction
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 5000m,
            currency: Currency.NGN);

        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        dbContext.PaymentAttempts.Add(attempt2);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentAttempt_UniqueConstraint_ProviderAndProviderReference_ShouldThrowOnDuplicateWhenNotNull()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var sharedProviderRef = $"FLW-SHARED-{Guid.NewGuid():N}";

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: Guid.NewGuid(),
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 3000m,
            currency: Currency.NGN);
        attempt1.MarkProcessing();
        attempt1.MarkSucceeded(sharedProviderRef);

        var attempt2 = PaymentAttempt.Create(
            ledgerTransactionId: Guid.NewGuid(),
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 1,
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 3000m,
            currency: Currency.NGN);
        attempt2.MarkProcessing();
        attempt2.MarkSucceeded(sharedProviderRef); // Duplicate provider reference for Flutterwave

        dbContext.PaymentAttempts.Add(attempt1);
        await dbContext.SaveChangesAsync();

        // Act & Assert
        dbContext.PaymentAttempts.Add(attempt2);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task PaymentAttempt_UniqueConstraint_ProviderAndNullProviderReference_ShouldAllowMultipleNulls()
    {
        // Arrange (Filtered unique index allows multiple records with null ProviderReference)
        await using var dbContext = await CreateDbContextAsync();

        var attempt1 = PaymentAttempt.Create(
            ledgerTransactionId: Guid.NewGuid(),
            provider: PaymentProvider.Paystack,
            attemptNumber: 1,
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 10000m,
            currency: Currency.NGN);

        var attempt2 = PaymentAttempt.Create(
            ledgerTransactionId: Guid.NewGuid(),
            provider: PaymentProvider.Paystack,
            attemptNumber: 1,
            requestReference: $"CBZPA-{Guid.NewGuid():N}",
            amount: 20000m,
            currency: Currency.NGN);

        // Act & Assert: Both have null ProviderReference and should save without unique collision
        dbContext.PaymentAttempts.AddRange(attempt1, attempt2);
        var savedCount = await dbContext.SaveChangesAsync();

        Assert.Equal(2, savedCount);
    }

    [Fact]
    public async Task PaymentAttempt_StateTransitions_ShouldPersistAcrossDatabaseTransactions()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();
        var requestRef = $"CBZPA-{Guid.NewGuid():N}";
        var providerRef = $"PSTK-{Guid.NewGuid():N}";

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxId,
            provider: PaymentProvider.Paystack,
            attemptNumber: 1,
            requestReference: requestRef,
            amount: 7500m,
            currency: Currency.NGN);

        dbContext.PaymentAttempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        // Step 1: Transition to Processing
        await using var updateContext1 = await CreateDbContextAsync();
        var attemptToProcess = await updateContext1.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        attemptToProcess.MarkProcessing();
        await updateContext1.SaveChangesAsync();

        // Verify Processing in fresh context
        await using var verifyContext1 = await CreateDbContextAsync();
        var processed = await verifyContext1.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        Assert.Equal(PaymentAttemptStatus.Processing, processed.Status);
        Assert.NotNull(processed.StartedAtUtc);
        Assert.Null(processed.CompletedAtUtc);

        // Step 2: Transition to Succeeded
        await using var updateContext2 = await CreateDbContextAsync();
        var attemptToSucceed = await updateContext2.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        attemptToSucceed.MarkSucceeded(providerRef, safeMetadata: "{\"settled\":true}");
        await updateContext2.SaveChangesAsync();

        // Verify Succeeded in fresh context
        await using var verifyContext2 = await CreateDbContextAsync();
        var succeeded = await verifyContext2.PaymentAttempts.FirstAsync(p => p.Id == attempt.Id);
        Assert.Equal(PaymentAttemptStatus.Succeeded, succeeded.Status);
        Assert.Equal(providerRef, succeeded.ProviderReference);
        Assert.NotNull(succeeded.CompletedAtUtc);
        Assert.Equal("{\"settled\":true}", succeeded.SafeMetadata);
    }
}
