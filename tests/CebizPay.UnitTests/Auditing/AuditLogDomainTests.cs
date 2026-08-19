using CebizPay.Domain.Auditing;
using CebizPay.Domain.Entities;
using Xunit;

namespace CebizPay.UnitTests.Auditing;

public sealed class AuditLogDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateAuditLog()
    {
        // Arrange
        var actorId = "user-123";
        var action = AuditActions.PeerTransferCompleted;
        var resourceType = AuditResourceTypes.PeerTransfer;
        var resourceId = "transfer-456";
        var orgId = Guid.NewGuid();
        var beforeJson = "{\"status\":\"PENDING\"}";
        var afterJson = "{\"status\":\"COMPLETED\"}";
        var ip = "192.168.1.1";
        var userAgent = "Mozilla/5.0";
        var correlationId = "corr-789";

        // Act
        var audit = AuditLog.Create(
            actorId: actorId,
            action: action,
            resourceType: resourceType,
            resourceId: resourceId,
            organizationId: orgId,
            beforeJson: beforeJson,
            afterJson: afterJson,
            ipAddress: ip,
            userAgent: userAgent,
            correlationId: correlationId);

        // Assert
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal(action, audit.Action);
        Assert.Equal(resourceType, audit.ResourceType);
        Assert.Equal(resourceId, audit.ResourceId);
        Assert.Equal(orgId, audit.OrganizationId);
        Assert.Equal(beforeJson, audit.BeforeJson);
        Assert.Equal(afterJson, audit.AfterJson);
        Assert.Equal(ip, audit.IpAddress);
        Assert.Equal(userAgent, audit.UserAgent);
        Assert.Equal(correlationId, audit.CorrelationId);
        Assert.True((DateTime.UtcNow - audit.OccurredAtUtc).TotalSeconds < 5);
    }

    [Theory]
    [InlineData("", "USER_REGISTERED", "USER")]
    [InlineData("   ", "USER_REGISTERED", "USER")]
    [InlineData(null, "USER_REGISTERED", "USER")]
    [InlineData("actor-1", "", "USER")]
    [InlineData("actor-1", "   ", "USER")]
    [InlineData("actor-1", null, "USER")]
    [InlineData("actor-1", "USER_REGISTERED", "")]
    [InlineData("actor-1", "USER_REGISTERED", "   ")]
    [InlineData("actor-1", "USER_REGISTERED", null)]
    public void Create_WithMissingRequiredParameters_ShouldThrowArgumentException(
        string? actorId, string? action, string? resourceType)
    {
        Assert.Throws<ArgumentException>(() =>
            AuditLog.Create(
                actorId: actorId!,
                action: action!,
                resourceType: resourceType!));
    }

    [Fact]
    public void BackwardCompatibility_ConstructorAndAliases_ShouldWorkProperly()
    {
        // Arrange & Act
        var audit = new AuditLog(
            actorUserId: "admin-1",
            action: "Kyc.Verify",
            entityType: "IndividualProfile",
            entityId: "user-2",
            detailsJson: "{\"reason\":\"Verified\"}");

        // Assert
        Assert.Equal("admin-1", audit.ActorId);
        Assert.Equal("admin-1", audit.ActorUserId);
        Assert.Equal("Kyc.Verify", audit.Action);
        Assert.Equal("IndividualProfile", audit.ResourceType);
        Assert.Equal("IndividualProfile", audit.EntityType);
        Assert.Equal("user-2", audit.ResourceId);
        Assert.Equal("user-2", audit.EntityId);
        Assert.Equal("{\"reason\":\"Verified\"}", audit.AfterJson);
        Assert.Equal("{\"reason\":\"Verified\"}", audit.DetailsJson);
        Assert.Equal(audit.OccurredAtUtc, audit.CreatedAtUtc);
    }
}
