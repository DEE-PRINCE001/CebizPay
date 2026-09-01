using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Payments;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Payments.Entities;
using CebizPay.Domain.Payments.Enums;
using CebizPay.Infrastructure.Payments.Common;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Payments;

public sealed class WebhookProcessingServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebhookProcessor _mockFinancialProcessor = Substitute.For<IWebhookProcessor>();
    private readonly IComplianceWebhookProcessor _mockComplianceProcessor = Substitute.For<IComplianceWebhookProcessor>();
    private readonly ReconciliationMetrics _metrics = new();
    private readonly WebhookProcessingService _sut;

    public WebhookProcessingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new ApplicationDbContext(options);

        _sut = new WebhookProcessingService(
            _dbContext,
            _mockFinancialProcessor,
            _mockComplianceProcessor,
            _metrics,
            NullLogger<WebhookProcessingService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ProcessPendingWebhooksBatch_ClaimsAndProcessesFinancialAndComplianceWebhooks()
    {
        var financialEvt = WebhookEvent.Create(
            provider: PaymentProvider.Monnify,
            providerEventId: "MNFY_EVT_100",
            eventType: "SUCCESSFUL_TRANSACTION",
            payloadHash: "hash123",
            correlationReference: "REF-001");
        _dbContext.WebhookEvents.Add(financialEvt);

        var complianceEvt = ComplianceWebhookEvent.Create(
            provider: VerificationProvider.Dojah,
            providerEventId: "DOJAH_EVT_100",
            eventType: "kyc_bvn_verify",
            payloadHash: "hash456",
            correlationReference: "KYC-REF-001");
        _dbContext.ComplianceWebhookEvents.Add(complianceEvt);

        await _dbContext.SaveChangesAsync();

        var processedCount = await _sut.ProcessPendingWebhooksBatchAsync(50);

        Assert.Equal(2, processedCount);

        var refreshedFin = await _dbContext.WebhookEvents.FindAsync(financialEvt.Id);
        Assert.NotNull(refreshedFin);
        Assert.Equal(WebhookEventStatus.Processed, refreshedFin.Status);

        var refreshedComp = await _dbContext.ComplianceWebhookEvents.FindAsync(complianceEvt.Id);
        Assert.NotNull(refreshedComp);
        Assert.Equal(ComplianceWebhookEventStatus.Processed, refreshedComp.Status);
    }

    [Fact]
    public async Task ProcessSingleFinancialWebhook_WhenAlreadyProcessed_ReturnsProcessedIdempotently()
    {
        var evt = WebhookEvent.Create(
            provider: PaymentProvider.Flutterwave,
            providerEventId: "FLW_EVT_200",
            eventType: "transfer.completed",
            payloadHash: "hash789");
        evt.MarkProcessed(Guid.NewGuid());
        _dbContext.WebhookEvents.Add(evt);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ProcessSingleFinancialWebhookAsync(evt.Id);

        Assert.Equal(WebhookProcessingStatus.Processed, result.Status);
        Assert.Equal("FLW_EVT_200", result.ProviderEventId);
    }
}
