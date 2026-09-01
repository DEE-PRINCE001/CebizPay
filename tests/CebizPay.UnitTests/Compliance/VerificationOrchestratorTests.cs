#pragma warning disable CS1591, CA1822
using CebizPay.Application.Common.Interfaces.Compliance;
using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Compliance.Entities;
using CebizPay.Domain.Compliance.Enums;
using CebizPay.Domain.Compliance.Events;
using CebizPay.Infrastructure.Compliance.Common;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Compliance;

public sealed class VerificationOrchestratorTests
{
    private readonly IVerificationRoutingService _routingService = Substitute.For<IVerificationRoutingService>();
    private readonly IVerificationProviderFactory _factory = Substitute.For<IVerificationProviderFactory>();
    private readonly IOutboxService _outboxService = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task VerifyBvnAsync_WhenPrimarySucceeds_CompletesAndStops()
    {
        using var dbContext = CreateDbContext();

        _routingService.ResolvePrimaryProvider(VerificationCapability.Identity).Returns(VerificationProvider.Dojah);

        var dojahProvider = Substitute.For<IIdentityVerificationProvider>();
        dojahProvider.Provider.Returns(VerificationProvider.Dojah);
        dojahProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.Match("ref_dojah_1", 100m, "Matched"));

        _factory.GetIdentityVerificationProvider(VerificationProvider.Dojah).Returns(dojahProvider);

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            _routingService,
            _factory,
            _outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var response = await orchestrator.VerifyBvnAsync("user_1", "22222222222", "Emeka", "Okonkwo");

        Assert.Equal(VerificationStatus.Completed, response.Status);
        Assert.Equal(VerificationResultStatus.Match, response.LatestResultStatus);
        Assert.False(response.UsedFallback);
        Assert.Equal(VerificationProvider.Dojah, response.ActiveProvider);

        _outboxService.Received(1).Write(Arg.Any<VerificationInitiatedDomainEvent>());
        _outboxService.Received(1).Write(Arg.Any<VerificationCompletedDomainEvent>());
    }

    [Fact]
    public async Task VerifyBvnAsync_WhenMismatch_StopsWithoutFailover()
    {
        using var dbContext = CreateDbContext();

        _routingService.ResolvePrimaryProvider(VerificationCapability.Identity).Returns(VerificationProvider.Dojah);

        var dojahProvider = Substitute.For<IIdentityVerificationProvider>();
        dojahProvider.Provider.Returns(VerificationProvider.Dojah);
        dojahProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.Mismatch("NAME_MISMATCH", "Names do not match"));

        _factory.GetIdentityVerificationProvider(VerificationProvider.Dojah).Returns(dojahProvider);

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            _routingService,
            _factory,
            _outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var response = await orchestrator.VerifyBvnAsync("user_1", "22222222222", "Emeka", "Okonkwo");

        Assert.Equal(VerificationStatus.Failed, response.Status);
        Assert.Equal(VerificationResultStatus.Mismatch, response.LatestResultStatus);
        Assert.False(response.UsedFallback);

        // Verification routing service was NOT asked for fallback on mismatch
        _routingService.DidNotReceive().GetNextFallbackProvider(Arg.Any<VerificationCapability>(), Arg.Any<VerificationProvider>());
    }

    [Fact]
    public async Task VerifyBvnAsync_WhenTechnicalFailure_FailsOverToFallbackProvider()
    {
        using var dbContext = CreateDbContext();

        _routingService.ResolvePrimaryProvider(VerificationCapability.Identity).Returns(VerificationProvider.Dojah);
        _routingService.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.Dojah).Returns(VerificationProvider.SmileId);
        _routingService.GetNextFallbackProvider(VerificationCapability.Identity, VerificationProvider.SmileId).Returns((VerificationProvider?)null);

        var dojahProvider = Substitute.For<IIdentityVerificationProvider>();
        dojahProvider.Provider.Returns(VerificationProvider.Dojah);
        dojahProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.TechnicalFailure("HTTP_500", "Internal Server Error"));

        var smileIdProvider = Substitute.For<IIdentityVerificationProvider>();
        smileIdProvider.Provider.Returns(VerificationProvider.SmileId);
        smileIdProvider.VerifyBvnAsync("22222222222", "Emeka", "Okonkwo", Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(VerificationProviderResult.Match("ref_smile_2", 99m, "Matched"));

        _factory.GetIdentityVerificationProvider(VerificationProvider.Dojah).Returns(dojahProvider);
        _factory.GetIdentityVerificationProvider(VerificationProvider.SmileId).Returns(smileIdProvider);

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            _routingService,
            _factory,
            _outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var response = await orchestrator.VerifyBvnAsync("user_1", "22222222222", "Emeka", "Okonkwo");

        Assert.Equal(VerificationStatus.Completed, response.Status);
        Assert.Equal(VerificationResultStatus.Match, response.LatestResultStatus);
        Assert.True(response.UsedFallback);
        Assert.Equal(VerificationProvider.SmileId, response.ActiveProvider);
        Assert.Equal(VerificationProvider.Dojah, response.PrimaryProvider);
        Assert.Equal(2, response.Evidences.Count);

        _outboxService.Received(1).Write(Arg.Any<VerificationFallbackUsedDomainEvent>());
        _outboxService.Received(1).Write(Arg.Any<VerificationCompletedDomainEvent>());
    }

    [Fact]
    public async Task VerifyBvnAsync_WithExistingIdempotencyKey_ReplaysPreviousResult()
    {
        using var dbContext = CreateDbContext();

        const string idempotencyKey = "idemp_kyc_bvn_9999";
        var existingOp = VerificationOperation.Create(
            "CBZKYC-20260830120000-EXISTING",
            VerificationType.IndividualKyc,
            VerificationCapability.Identity,
            VerificationProvider.Dojah,
            userId: "user_1",
            idempotencyKey: idempotencyKey);

        existingOp.MarkCompleted();
        existingOp.AddEvidence(VerificationEvidence.Create(
            existingOp.Id,
            existingOp.VerificationType,
            existingOp.Capability,
            VerificationProvider.Dojah,
            VerificationResultStatus.Match,
            userId: "user_1",
            confidenceScore: 100m));

        dbContext.VerificationOperations.Add(existingOp);
        await dbContext.SaveChangesAsync();

        var orchestrator = new VerificationOrchestrator(
            dbContext,
            _routingService,
            _factory,
            _outboxService,
            NullLogger<VerificationOrchestrator>.Instance);

        var response = await orchestrator.VerifyBvnAsync("user_1", "22222222222", "Emeka", "Okonkwo", idempotencyKey: idempotencyKey);

        Assert.Equal(existingOp.Reference, response.Reference);
        Assert.Equal(VerificationStatus.Completed, response.Status);

        // Factory was not called because idempotent result was replayed
        _factory.DidNotReceive().GetIdentityVerificationProvider(Arg.Any<VerificationProvider>());
    }
}
