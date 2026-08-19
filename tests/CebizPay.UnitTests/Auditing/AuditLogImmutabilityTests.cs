using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class AuditLogImmutabilityTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task AuditLog_AddAndSave_ShouldSucceed()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var audit = AuditLog.Create(
            actorId: "actor-1",
            action: AuditActions.FeePolicyCreated,
            resourceType: AuditResourceTypes.FeePolicy,
            resourceId: "policy-1");

        // Act
        dbContext.AuditLogs.Add(audit);
        var savedCount = await dbContext.SaveChangesAsync();

        // Assert
        Assert.Equal(1, savedCount);
        var retrieved = await dbContext.AuditLogs.FindAsync(audit.Id);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task AuditLog_AttemptToUpdate_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var audit = AuditLog.Create(
            actorId: "actor-1",
            action: AuditActions.UserRegistered,
            resourceType: AuditResourceTypes.User,
            resourceId: "user-1");

        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync();

        // Act: Manually mark entry as modified
        dbContext.Entry(audit).State = EntityState.Modified;

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuditLog_AttemptToDelete_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var audit = AuditLog.Create(
            actorId: "actor-1",
            action: AuditActions.UserRegistered,
            resourceType: AuditResourceTypes.User,
            resourceId: "user-1");

        dbContext.AuditLogs.Add(audit);
        await dbContext.SaveChangesAsync();

        // Act: Attempt to remove entry
        dbContext.AuditLogs.Remove(audit);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
