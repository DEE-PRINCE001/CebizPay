#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Infrastructure.Compliance.Common;
using CebizPay.Infrastructure.Compliance.Dojah;
using CebizPay.Infrastructure.Compliance.Ninja;
using CebizPay.Infrastructure.Compliance.SmileId;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Compliance;

public sealed class ComplianceWebhookIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public ComplianceWebhookIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
    }

    private async Task<ApplicationDbContext> CreateDbContextAsync()
    {
        var connectionString = _fixture.PostgresContainer.GetConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    [Fact]
    public async Task ProcessWebhookAsync_PostgresDeduplication_HandlesDuplicateEventsSafely()
    {
        using var dbContext = await CreateDbContextAsync();

        var signatureVerifier = Substitute.For<IComplianceWebhookSignatureVerifier>();
        signatureVerifier.VerifySignature(Arg.Any<VerificationProvider>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<string>())
            .Returns(true);

        var outboxService = Substitute.For<IOutboxService>();

        var processor = new ComplianceWebhookProcessor(
            dbContext,
            signatureVerifier,
            outboxService,
            Options.Create(new DojahOptions { Enabled = true, WebhookSecret = "secret" }),
            Options.Create(new SmileIdOptions { Enabled = true, WebhookSecret = "secret" }),
            Options.Create(new NinjaOptions { Enabled = true, WebhookSecret = "secret" }),
            NullLogger<ComplianceWebhookProcessor>.Instance);

        var eventId = $"evt_pg_{Guid.NewGuid():N}";
        var payload = $"{{\"event_id\":\"{eventId}\",\"event\":\"kyc.completed\",\"reference\":\"ref_123\"}}";

        // First delivery
        var result1 = await processor.ProcessWebhookAsync(VerificationProvider.Dojah, payload, new Dictionary<string, string>());
        Assert.Equal(ComplianceWebhookProcessingStatus.Processed, result1.Status);

        // Second delivery (duplicate)
        var result2 = await processor.ProcessWebhookAsync(VerificationProvider.Dojah, payload, new Dictionary<string, string>());
        Assert.Equal(ComplianceWebhookProcessingStatus.Duplicate, result2.Status);

        // Verify single record in PostgreSQL
        var count = await dbContext.ComplianceWebhookEvents
            .CountAsync(e => e.Provider == VerificationProvider.Dojah && e.ProviderEventId == eventId);

        Assert.Equal(1, count);
    }
}
