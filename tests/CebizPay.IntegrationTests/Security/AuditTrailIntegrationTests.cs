using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Application.UseCases.Organizations.UpdateStatus;
using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Security;

[Collection("Infrastructure")]
public sealed class AuditTrailIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public AuditTrailIntegrationTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private ApplicationDbContext CreateContext()
    {
        var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_fixture.PostgresContainer.GetConnectionString())
            .Options;
        return new ApplicationDbContext(opts);
    }

    [Fact]
    public async Task OrganizationStatusUpdate_ShouldPersistAuditRecordInSameTransaction()
    {
        // Arrange
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var adminId = $"admin_{Guid.NewGuid():N}";
        var org = new Organization($"Org-{Guid.NewGuid():N}", "org@example.com", "+2348012345678");
        org.TransitionStatus(OrganizationStatus.Verified);
        db.Organizations.Add(org);
        await db.SaveChangesAsync();

        var publisher = Substitute.For<IEventPublisher>();
        var handler = new UpdateOrganizationStatusCommandHandler(db, publisher);

        var command = new UpdateOrganizationStatusCommand(
            OrganizationId: org.Id,
            NewStatus: OrganizationStatus.Suspended,
            Reason: "Suspicious activities detected",
            AdminUserId: adminId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(OrganizationStatus.Suspended.ToString(), result.Status);

        var auditLog = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.ResourceId == org.Id.ToString() && a.Action == AuditActions.OrganizationSuspended);

        Assert.NotNull(auditLog);
        Assert.Equal(adminId, auditLog.ActorId);
        Assert.Equal(AuditResourceTypes.Organization, auditLog.ResourceType);
        Assert.Equal(org.Id, auditLog.OrganizationId);
        Assert.NotNull(auditLog.AfterJson);
        Assert.Contains("Suspicious activities detected", auditLog.AfterJson);
    }

    [Fact]
    public async Task AuditLog_PostgreSqlImmutability_AttemptUpdate_ShouldThrow()
    {
        // Arrange
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var audit = AuditLog.Create(
            actorId: "actor-immutable",
            action: AuditActions.FeePolicyCreated,
            resourceType: AuditResourceTypes.FeePolicy,
            resourceId: "policy-test");

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        // Act: Attempt to modify entry
        db.Entry(audit).State = EntityState.Modified;

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditLog_PostgreSqlImmutability_AttemptDelete_ShouldThrow()
    {
        // Arrange
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();

        var audit = AuditLog.Create(
            actorId: "actor-immutable-del",
            action: AuditActions.FeePolicyCreated,
            resourceType: AuditResourceTypes.FeePolicy,
            resourceId: "policy-test-del");

        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync();

        // Act: Attempt to delete entry
        db.AuditLogs.Remove(audit);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
