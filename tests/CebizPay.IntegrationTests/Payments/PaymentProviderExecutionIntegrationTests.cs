using System.Net;
using System.Text;
using System.Text.Json;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Payments.Flutterwave;
using CebizPay.Infrastructure.Payments.Monnify;
using CebizPay.Infrastructure.Payments.Monnify.Models;
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
        HttpMessageHandler mnfyHandler,
        HttpMessageHandler flwHandler,
        HttpMessageHandler pstkHandler)
    {
        var mnfyOptions = Microsoft.Extensions.Options.Options.Create(new MonnifyOptions
        {
            BaseUrl = "https://sandbox.monnify.com",
            ApiKey = "MK_TEST",
            SecretKey = "SK_TEST",
            ContractCode = "12345",
            SourceAccountNumber = "7820123456",
            Enabled = true
        });
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

        var mnfyClient = new MonnifyClient(new HttpClient(mnfyHandler) { BaseAddress = new Uri("https://sandbox.monnify.com/") }, mnfyOptions, NullLogger<MonnifyClient>.Instance);
        var flwClient = new FlutterwaveClient(new HttpClient(flwHandler) { BaseAddress = new Uri("https://api.flutterwave.com/") }, flwOptions, NullLogger<FlutterwaveClient>.Instance);
        var pstkClient = new PaystackClient(new HttpClient(pstkHandler) { BaseAddress = new Uri("https://api.paystack.co/") }, pstkOptions, NullLogger<PaystackClient>.Instance);

        var mnfyProvider = new MonnifyPaymentProvider(mnfyClient, dbContext, mnfyOptions, NullLogger<MonnifyPaymentProvider>.Instance);
        var flwProvider = new FlutterwavePaymentProvider(flwClient, dbContext, NullLogger<FlutterwavePaymentProvider>.Instance);
        var pstkProvider = new PaystackPaymentProvider(pstkClient, dbContext, NullLogger<PaystackPaymentProvider>.Instance);

        var providerFactory = new PaymentProviderFactory(new IPaymentProvider[] { mnfyProvider, flwProvider, pstkProvider });
        var routingService = new PaymentRoutingService();
        var outboxService = new OutboxService(dbContext);

        var executor = new PaymentProviderBankTransferExecutor(
            providerFactory,
            routingService,
            dbContext,
            null,
            outboxService,
            NullLogger<PaymentProviderBankTransferExecutor>.Instance);

        return (executor, dbContext);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulMonnifyResponse_ShouldCreateSucceededAttemptAndOutboxMessages()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var ledgerTxId = Guid.NewGuid();
        var bankTransfer = BankTransfer.CreatePending(
            ledgerTransactionId: ledgerTxId,
            senderWalletId: Guid.NewGuid(),
            destinationBankCode: "058",
            destinationAccountNumber: "0690000031",
            destinationAccountName: "Alice Chukwu",
            amount: 15000m,
            currency: Currency.NGN,
            feeAmount: 50m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"BT-{Guid.NewGuid():N}"[..18]);

        dbContext.BankTransfers.Add(bankTransfer);
        await dbContext.SaveChangesAsync();

        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "tok_test", ExpiresIn = 3600 }
        });
        var disbJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifySingleTransferResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifySingleTransferResponseBody
            {
                Reference = bankTransfer.Reference,
                TransactionReference = "MNFY_TX_889900",
                Amount = 15000m,
                Currency = "NGN",
                Status = "SUCCESS"
            }
        });

        var mnfyHandler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.OK, disbJson));
        var flwHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var pstkHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");

        var (executor, _) = CreateExecutor(dbContext, mnfyHandler, flwHandler, pstkHandler);

        // Act
        await executor.ExecuteAsync(bankTransfer);

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var attempt = await readContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.LedgerTransactionId == ledgerTxId && p.AttemptNumber == 1);

        Assert.NotNull(attempt);
        Assert.Equal(PaymentAttemptStatus.Succeeded, attempt.Status);
        Assert.Equal("MNFY_TX_889900", attempt.ProviderReference);
        Assert.Equal(PaymentProvider.Monnify, attempt.Provider);
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
            destinationBankCode: "058",
            destinationAccountNumber: "0000000000",
            destinationAccountName: "Invalid Account",
            amount: 5000m,
            currency: Currency.NGN,
            feeAmount: 0m,
            feePolicyId: null,
            feePolicyVersion: null,
            reference: $"BT-{Guid.NewGuid():N}"[..18]);

        dbContext.BankTransfers.Add(bankTransfer);
        await dbContext.SaveChangesAsync();

        var authJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifyAuthResponseBody>
        {
            RequestSuccessful = true,
            ResponseBody = new MonnifyAuthResponseBody { AccessToken = "tok_test", ExpiresIn = 3600 }
        });
        var disbJson = JsonSerializer.Serialize(new MonnifyApiResponse<MonnifySingleTransferResponseBody>
        {
            RequestSuccessful = false,
            ResponseCode = "INVALID_ACCOUNT",
            ResponseMessage = "Account number is invalid or closed."
        });

        var mnfyHandler = new SequentialMockHttpMessageHandler(
            (HttpStatusCode.OK, authJson),
            (HttpStatusCode.BadRequest, disbJson));
        var flwHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var pstkHandler = new IntegrationMockHttpMessageHandler(HttpStatusCode.OK, "{}");

        var (executor, _) = CreateExecutor(dbContext, mnfyHandler, flwHandler, pstkHandler);

        // Act
        await executor.ExecuteAsync(bankTransfer);

        // Assert in fresh DbContext
        await using var readContext = await CreateDbContextAsync();
        var attempt = await readContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.LedgerTransactionId == ledgerTxId && p.AttemptNumber == 1);

        Assert.NotNull(attempt);
        Assert.Equal(PaymentAttemptStatus.Failed, attempt.Status);
        Assert.Equal("INVALID_ACCOUNT", attempt.FailureCode);
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

    private sealed class SequentialMockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode StatusCode, string Content)> _responses;

        public SequentialMockHttpMessageHandler(params (HttpStatusCode, string)[] responses)
        {
            _responses = new Queue<(HttpStatusCode, string)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var next = _responses.Dequeue();
            var response = new HttpResponseMessage(next.StatusCode)
            {
                Content = new StringContent(next.Content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
