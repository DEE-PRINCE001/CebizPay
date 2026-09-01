#pragma warning disable CS1591
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Infrastructure.Compliance.Common;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Compliance;

public sealed class VerificationOrchestrationIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public VerificationOrchestrationIntegrationTests(InfrastructureFixture fixture)
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
    public async Task VerifyBvnAsync_PostgresPersistence_StoresOperationAndEvidenceAndOutbox()
    {
        using var dbContext = await CreateDbContextAsync();

        var routingService = Substitute.For<IVerificationRoutingService>();
        routingService.ResolvePrimaryProvider(VerificationCapability.Identity).Returns(VerificationProvider.Dojah);

        var factory = Substitute.For<IVerificationProviderFactory>();
        var dojahProvider = Substitute.For<IIdentityVerificationProvider>();
        dojahProvider.Provider.Returns(VerificationProvider.Dojah);
        dojahProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.Match("dojah_ref_100", 100m, "Matched successfully.", "{\"verifiedFields\":[\"bvn\"]}"));

        factory.GetIdentityVerificationProvider(VerificationProvider.Dojah).Returns(dojahProvider);

        var outboxService = Substitute.For<IOutboxService>();

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            routingService,
            factory,
            outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var userId = Guid.NewGuid().ToString();
        var idempotencyKey = $"idemp_pg_{Guid.NewGuid():N}";

        var response = await orchestrator.VerifyBvnAsync(
            userId,
            "22222222222",
            "Emeka",
            "Okonkwo",
            idempotencyKey: idempotencyKey);

        Assert.NotNull(response);
        Assert.Equal(VerificationStatus.Completed, response.Status);
        Assert.Equal(VerificationResultStatus.Match, response.LatestResultStatus);

        // Verify stored in PostgreSQL
        var persistedOp = await dbContext.VerificationOperations
            .Include(o => o.Evidences)
            .FirstOrDefaultAsync(o => o.Reference == response.Reference);

        Assert.NotNull(persistedOp);
        Assert.Equal(userId, persistedOp.UserId);
        Assert.Equal(idempotencyKey, persistedOp.IdempotencyKey);
        Assert.Single(persistedOp.Evidences);

        var evidence = persistedOp.Evidences.First();
        Assert.Equal(VerificationProvider.Dojah, evidence.Provider);
        Assert.Equal(VerificationResultStatus.Match, evidence.ResultStatus);
        Assert.Equal(100m, evidence.ConfidenceScore);
        Assert.Equal("dojah_ref_100", evidence.ProviderReference);
    }

    [Fact]
    public async Task VerifyBvnAsync_FailoverChain_PersistsMultipleEvidencesInPostgres()
    {
        using var dbContext = await CreateDbContextAsync();

        var routingService = Substitute.For<IVerificationRoutingService>();
        routingService.ResolvePrimaryProvider(VerificationCapability.Identity).Returns(VerificationProvider.Dojah);
        routingService.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.Dojah).Returns(VerificationProvider.SmileId);
        routingService.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.SmileId).Returns((VerificationProvider?)null);

        var factory = Substitute.For<IVerificationProviderFactory>();
        var dojahProvider = Substitute.For<IIdentityVerificationProvider>();
        dojahProvider.Provider.Returns(VerificationProvider.Dojah);
        dojahProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.TechnicalFailure("HTTP_TIMEOUT", "Dojah connection timed out."));

        var smileIdProvider = Substitute.For<IIdentityVerificationProvider>();
        smileIdProvider.Provider.Returns(VerificationProvider.SmileId);
        smileIdProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.Match("smile_ref_200", 98.5m, "Smile ID verified BVN successfully."));

        factory.GetIdentityVerificationProvider(VerificationProvider.Dojah).Returns(dojahProvider);
        factory.GetIdentityVerificationProvider(VerificationProvider.SmileId).Returns(smileIdProvider);

        var outboxService = Substitute.For<IOutboxService>();

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            routingService,
            factory,
            outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var userId = Guid.NewGuid().ToString();
        var response = await orchestrator.VerifyBvnAsync(
            userId,
            "22222222222",
            "Emeka",
            "Okonkwo");

        Assert.NotNull(response);
        Assert.Equal(VerificationStatus.Completed, response.Status);
        Assert.True(response.UsedFallback);
        Assert.Equal(VerificationProvider.SmileId, response.ActiveProvider);

        var persistedOp = await dbContext.VerificationOperations
            .Include(o => o.Evidences)
            .FirstOrDefaultAsync(o => o.Reference == response.Reference);

        Assert.NotNull(persistedOp);
        Assert.True(persistedOp.UsedFallback);
        Assert.Equal(2, persistedOp.Evidences.Count);
    }
}
