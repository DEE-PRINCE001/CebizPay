using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Domain.Payments.Events;
using CebizPay.Infrastructure.Messaging;
using CebizPay.Infrastructure.Options;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

/// <summary>
/// Unit tests for <see cref="PaymentFailoverWorker"/> verifying RabbitMQ message processing,
/// business failure termination guards, and automated failover dispatch.
/// </summary>
public sealed class PaymentFailoverWorkerTests
{
    private readonly IRabbitMqConnectionProvider _connectionProvider = Substitute.For<IRabbitMqConnectionProvider>();
    private readonly IPaymentFailoverService _failoverService = Substitute.For<IPaymentFailoverService>();
    private readonly IOptions<RabbitMQOptions> _options = Microsoft.Extensions.Options.Options.Create(new RabbitMQOptions());

    private static ApplicationDbContext CreateDbContext()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(dbOptions);
    }

    private PaymentFailoverWorker CreateWorker(ApplicationDbContext dbContext)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => dbContext);
        services.AddScoped(_ => _failoverService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        return new PaymentFailoverWorker(
            scopeFactory,
            _connectionProvider,
            _options,
            NullLogger<PaymentFailoverWorker>.Instance);
    }

    [Theory]
    [InlineData("BUSINESS_REJECTION")]
    [InlineData("INVALID_ACCOUNT")]
    [InlineData("INVALID_BANK")]
    [InlineData("BLOCKED_ACCOUNT")]
    [InlineData("INSUFFICIENT_FUNDS")]
    public async Task ProcessMessageAsync_BusinessFailureCode_ShouldSkipFailover(string failureCode)
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var @event = new PaymentAttemptFailedEvent(
            PaymentAttemptId: Guid.NewGuid(),
            LedgerTransactionId: ledgerTxnId,
            Provider: PaymentProvider.Monnify,
            AttemptNumber: 1,
            RequestReference: "REF-001",
            FailureCode: failureCode,
            FailureReason: "Terminal business rejection",
            OccurredOnUtc: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(@event);

        // Act
        await worker.ProcessMessageAsync(json, CancellationToken.None);

        // Assert: Failover service must NOT be called for terminal business failures
        await _failoverService.DidNotReceive().FailoverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_BankTransferAlreadyCompleted_ShouldSkipFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Thor Odinson",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "BT-COMPLETED");
        transfer.MarkCompleted(DateTime.UtcNow, "PROV-REF-1");
        dbContext.BankTransfers.Add(transfer);
        await dbContext.SaveChangesAsync();

        var @event = new PaymentAttemptFailedEvent(
            PaymentAttemptId: Guid.NewGuid(),
            LedgerTransactionId: ledgerTxnId,
            Provider: PaymentProvider.Monnify,
            AttemptNumber: 1,
            RequestReference: "REF-002",
            FailureCode: "TECHNICAL_FAILURE",
            FailureReason: "Gateway Timeout",
            OccurredOnUtc: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(@event);

        // Act
        await worker.ProcessMessageAsync(json, CancellationToken.None);

        // Assert
        await _failoverService.DidNotReceive().FailoverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_AttemptAlreadySucceeded_ShouldSkipFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Bruce Banner",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "BT-SUCCEEDED");
        transfer.MarkProcessing();
        dbContext.BankTransfers.Add(transfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Flutterwave,
            attemptNumber: 2,
            requestReference: "REF-003-FLW",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        attempt.MarkSucceeded("FLW-SUCCESS");
        dbContext.PaymentAttempts.Add(attempt);

        await dbContext.SaveChangesAsync();

        var @event = new PaymentAttemptFailedEvent(
            PaymentAttemptId: Guid.NewGuid(),
            LedgerTransactionId: ledgerTxnId,
            Provider: PaymentProvider.Monnify,
            AttemptNumber: 1,
            RequestReference: "REF-003",
            FailureCode: "TECHNICAL_FAILURE",
            FailureReason: "Monnify connection reset",
            OccurredOnUtc: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(@event);

        // Act
        await worker.ProcessMessageAsync(json, CancellationToken.None);

        // Assert
        await _failoverService.DidNotReceive().FailoverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_LatestAttemptStatusUnknown_ShouldSkipFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Natasha Romanoff",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "BT-UNKNOWN");
        transfer.MarkProcessing();
        dbContext.BankTransfers.Add(transfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "REF-004-MNF",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        attempt.MarkUnknown("Gateway timeout waiting for response");
        dbContext.PaymentAttempts.Add(attempt);

        await dbContext.SaveChangesAsync();

        var @event = new PaymentAttemptFailedEvent(
            PaymentAttemptId: attempt.Id,
            LedgerTransactionId: ledgerTxnId,
            Provider: PaymentProvider.Monnify,
            AttemptNumber: 1,
            RequestReference: "REF-004-MNF",
            FailureCode: "TECHNICAL_FAILURE",
            FailureReason: "Monnify timeout",
            OccurredOnUtc: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(@event);

        // Act
        await worker.ProcessMessageAsync(json, CancellationToken.None);

        // Assert: Unknown must be reconciled first, never blindly failover
        await _failoverService.DidNotReceive().FailoverAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_ValidTechnicalFailure_ShouldExecuteFailover()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        var ledgerTxnId = Guid.NewGuid();
        var transfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxnId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "1234567890",
            destinationAccountName: "Peter Parker",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: "BT-FAILOVER");
        transfer.MarkProcessing();
        dbContext.BankTransfers.Add(transfer);

        var attempt = PaymentAttempt.Create(
            ledgerTransactionId: ledgerTxnId,
            provider: PaymentProvider.Monnify,
            attemptNumber: 1,
            requestReference: "REF-005-MNF",
            amount: 5000m,
            currency: Currency.NGN);
        attempt.MarkProcessing();
        attempt.MarkFailed("TECHNICAL_FAILURE", "Server returned 503 Service Unavailable");
        dbContext.PaymentAttempts.Add(attempt);

        await dbContext.SaveChangesAsync();

        _failoverService
            .FailoverAsync(ledgerTxnId, Arg.Any<CancellationToken>())
            .Returns(PaymentFailoverResult.Success(Guid.NewGuid(), PaymentProvider.Flutterwave, PaymentProviderResultStatus.Success));

        var @event = new PaymentAttemptFailedEvent(
            PaymentAttemptId: attempt.Id,
            LedgerTransactionId: ledgerTxnId,
            Provider: PaymentProvider.Monnify,
            AttemptNumber: 1,
            RequestReference: "REF-005-MNF",
            FailureCode: "TECHNICAL_FAILURE",
            FailureReason: "Server returned 503 Service Unavailable",
            OccurredOnUtc: DateTime.UtcNow);

        var json = JsonSerializer.Serialize(@event);

        // Act
        await worker.ProcessMessageAsync(json, CancellationToken.None);

        // Assert: Failover was invoked!
        await _failoverService.Received(1).FailoverAsync(ledgerTxnId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessMessageAsync_MalformedJson_ShouldThrowJsonException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var worker = CreateWorker(dbContext);

        // Act & Assert
        await Assert.ThrowsAnyAsync<JsonException>(() =>
            worker.ProcessMessageAsync("INVALID_NOT_JSON", CancellationToken.None));
    }
}
