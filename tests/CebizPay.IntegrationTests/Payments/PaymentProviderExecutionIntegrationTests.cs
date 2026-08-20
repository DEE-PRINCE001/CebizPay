using System.Net;
using System.Text;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Paystack;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CebizPay.IntegrationTests.Payments;

/// <summary>
/// PostgreSQL Testcontainers integration tests for <see cref="PaymentProviderBankTransferExecutor"/>
/// testing sequential attempt creation, status transitions, and outbox event persistence.
/// </summary>
public sealed class PaymentProviderExecutionIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public PaymentProviderExecutionIntegrationTests(InfrastructureFixture fixture)
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

    private static (PaymentProviderBankTransferExecutor Executor, ApplicationDbContext DbContext) CreateExecutor(
        ApplicationDbContext dbContext,
        HttpMessageHandler flwHandler,
        HttpMessageHandler pstkHandler)
    {
        var flwOptions = Microsoft.Extensions.Options.Options.Create(new FlutterwaveOptions
        {
            BaseUrl = "https://api.flutterwave.com",
            SecretKey = "FLWSECK_TEST-key"
        });
        var pstkOptions = Microsoft.Extensions.Options.Options.Create(new PaystackOptions
        {
            BaseUrl = "https://api.paystack.co",
            SecretKey = "sk_test_key"
        });

        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler) { BaseAddress = new Uri("https://api.flutterwave.com/") }, flwOptions, NullLogger<FlutterwaveClient>.Instance);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler) { BaseAddress = new Uri("https://api.paystack.co/") }, pstkOptions, NullLogger<PaystackClient>.Instance);

        var flwProvider = new FlutterwavePaymentProvider(flwClient, dbContext, NullLogger<FlutterwavePaymentProvider>.Instance);
        var pstkProvider = new PaystackPaymentProvider(pstkClient, dbContext, NullLogger<PaystackPaymentProvider>.Instance);

        var providerFactory = new PaymentProviderFactory(new IPaymentProvider[] { flwProvider, pstkProvider });
        var outboxService = new OutboxService(dbContext);

        var executor = new PaymentProviderBankTransferExecutor(
            providerFactory,
            dbContext,
            outboxService,
            NullLogger<PaymentProviderBankTransferExecutor>.Instance);

        return (executor, dbContext);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulGatewayResponse_ShouldCreateSucceededAttemptAndOutboxMessages()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();
        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "0690000031",
            destinationAccountName: "Pastor Bright",
            amount: 15000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"BT-{Guid.NewGuid():N}");

        dbContext.BankTransfers.Add(bankTransfer);
        await dbContext.SaveChangesAsync();

        var flwHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, """
        {
            "status": "success",
            "message": "Transfer Queued Successfully",
            "data": {
                "id": 998877,
                "status": "NEW",
                "reference": "CBZPA-REF",
                "bank_name": "ACCESS BANK"
            }
        }
        """);
        var pstkHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");

        var (executor, _) = CreateExecutor(dbContext, flwHandler, pstkHandler);

        // Act
        await executor.ExecuteAsync(bankTransfer);

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var attempt = await readContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.LedgerTransactionId == ledgerTxId && p.AttemptNumber == 1);

        Assert.NotNull(attempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("998877", attempt.ProviderReference);
        Assert.Equal(PaymentProvider.Flutterwave, attempt.Provider);
        Assert.Equal(15000m, attempt.Amount);
        Assert.Equal(Currency.NGN, attempt.Currency);
        Assert.NotNull(attempt.CompletedAtUtc);

        // Verify Outbox messages
        var outboxEvents = await readContext.OutboxMessages
            .Where(m => m.Type.Contains("PaymentAttempt"))
            .ToListAsync();

        Assert.Contains(outboxEvents, m => m.Type.Contains("PaymentAttemptProcessingEvent"));
        Assert.Contains(outboxEvents, m => m.Type.Contains("PaymentAttemptSucceededEvent"));
    }

    [Fact]
    public async Task ExecuteAsync_BusinessRejection_ShouldCreateFailedAttemptWithReason()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();
        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "044",
            destinationAccountNumber: "0000000000",
            destinationAccountName: "Invalid Account",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 0m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"BT-{Guid.NewGuid():N}");

        dbContext.BankTransfers.Add(bankTransfer);
        await dbContext.SaveChangesAsync();

        var flwHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.BadRequest, """
        {
            "status": "error",
            "message": "Account number is invalid or closed."
        }
        """);
        var pstkHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");

        var (executor, _) = CreateExecutor(dbContext, flwHandler, pstkHandler);

        // Act
        await executor.ExecuteAsync(bankTransfer);

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var attempt = await readContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.LedgerTransactionId == ledgerTxId && p.AttemptNumber == 1);

        Assert.NotNull(attempt);
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal("BUSINESS_REJECTION", attempt.FailureCode);
        Assert.Contains("Account number is invalid", attempt.FailureReason);
        Assert.NotNull(attempt.CompletedAtUtc);
    }

    private sealed class IntegrationMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public IntegrationMockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
