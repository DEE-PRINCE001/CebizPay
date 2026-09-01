#pragma warning disable CS1591, CA1822
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Infrastructure.Compliance.Common;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.SmileId;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class ComplianceWebhookProcessorTests
{
    private readonly IComplianceWebhookSignatureVerifier _signatureVerifier = Substitute.For<IComplianceWebhookSignatureVerifier>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();

    private readonly IOptions<DojahOptions> _dojahOptions = Options.Create(new DojahOptions
    {
        WebhookSecret = "dojah_secret_key",
        Enabled = true
    });

    private readonly IOptions<SmileIdOptions> _smileIdOptions = Options.Create(new SmileIdOptions
    {
        WebhookSecret = "smile_secret_key",
        ApiKey = "smile_api_key",
        Enabled = true
    });

    private readonly IOptions<NinjaOptions> _ninjaOptions = Options.Create(new NinjaOptions
    {
        WebhookSecret = "ninja_secret_key",
        Enabled = true
    });

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ProcessWebhookAsync_InvalidSignature_ReturnsInvalidSignature()
    {
        _signatureVerifier.VerifySignature(Arg.Any<VerificationProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(false);

        using var dbContext = CreateDbContext();
        var processor = new ComplianceWebhookProcessor(
            dbContext,
            _signatureVerifier,
            _outboxService,
            _dojahOptions,
            _smileIdOptions,
            _ninjaOptions,
            NullLogger<ComplianceWebhookProcessor>.Instance);

        var result = await processor.ProcessWebhookAsync(
            VerificationProvider.Dojah,
            "{\"event\":\"test\"}",
            new Dictionary<string, string>());

        Assert.Equal(ComplianceWebhookProcessingStatus.InvalidSignature, result.Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_DuplicateEvent_ReturnsDuplicate()
    {
        _signatureVerifier.VerifySignature(Arg.Any<VerificationProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        using var dbContext = CreateDbContext();

        var existingEvent = ComplianceWebhookEvent.Create(
            VerificationProvider.Dojah,
            "evt_dojah_12345",
            "verification.completed",
            "hash123");

        existingEvent.MarkProcessed();
        dbContext.ComplianceWebhookEvents.Add(existingEvent);
        await dbContext.SaveChangesAsync();

        var processor = new ComplianceWebhookProcessor(
            dbContext,
            _signatureVerifier,
            _outboxService,
            _dojahOptions,
            _smileIdOptions,
            _ninjaOptions,
            NullLogger<ComplianceWebhookProcessor>.Instance);

        var payload = "{\"event_id\":\"evt_dojah_12345\",\"event\":\"verification.completed\",\"reference\":\"CBZKYC-REF1\"}";
        var result = await processor.ProcessWebhookAsync(
            VerificationProvider.Dojah,
            payload,
            new Dictionary<string, string>());

        Assert.Equal(ComplianceWebhookProcessingStatus.Duplicate, result.Status);
    }

    [Fact]
    public async Task ProcessWebhookAsync_ValidSmileIdMatchCallback_UpdatesOperationAndWritesEvent()
    {
        _signatureVerifier.VerifySignature(Arg.Any<VerificationProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        using var dbContext = CreateDbContext();

        var op = VerificationOperation.Create(
            "CBZKYC-20260830120000-A1B2C3D4",
            VerificationType.IndividualKyc,
            VerificationCapability.Biometrics,
            VerificationProvider.SmileId,
            userId: "user_test_123");

        op.MarkPendingCallback();
        dbContext.VerificationOperations.Add(op);
        await dbContext.SaveChangesAsync();

        var processor = new ComplianceWebhookProcessor(
            dbContext,
            _signatureVerifier,
            _outboxService,
            _dojahOptions,
            _smileIdOptions,
            _ninjaOptions,
            NullLogger<ComplianceWebhookProcessor>.Instance);

        var payload = """
        {
            "JobId": "smile_job_777",
            "ResultCode": "0810",
            "ResultText": "Passed",
            "ConfidenceValue": "99.0",
            "PartnerParams": {
                "user_id": "CBZKYC-20260830120000-A1B2C3D4"
            }
        }
        """;

        var result = await processor.ProcessWebhookAsync(
            VerificationProvider.SmileId,
            payload,
            new Dictionary<string, string>());

        Assert.Equal(ComplianceWebhookProcessingStatus.Processed, result.Status);

        var updatedOp = await dbContext.VerificationOperations.Include(o => o.Evidences).FirstOrDefaultAsync(o => o.Id == op.Id);
        Assert.NotNull(updatedOp);
        Assert.Equal(VerificationStatus.Completed, updatedOp.Status);
        Assert.Single(updatedOp.Evidences);
        Assert.Equal(VerificationResultStatus.Match, updatedOp.Evidences.First().ResultStatus);

        _outboxService.Received(1).Write(Arg.Any<VerificationCompletedDomainEvent>());
    }
}
