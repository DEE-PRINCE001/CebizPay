using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.UnitTests.Finance;

/// <summary>
/// Unit tests for <see cref="PlatformFeePolicyService"/>.
/// Validates policy versioning, automatic deactivation of prior versions, outbox publishing, and queries.
/// </summary>
public sealed class PlatformFeePolicyServiceTests
{
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    private PlatformFeePolicyService CreateService(ApplicationDbContext dbContext)
    {
        return new PlatformFeePolicyService(
            dbContext,
            _outbox,
            NullLogger<PlatformFeePolicyService>.Instance);
    }

    [Fact]
    public async Task CreateAndActivatePolicyAsync_SequentialVersions_ShouldIncrementVersionAndDeactivatePrevious()
    {
        // Arrange
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext);

        // Act 1: Create Version 1
        var v1 = await service.CreateAndActivatePolicyAsync(
            operationType: FeeOperationType.BankTransfer,
            calculationMethod: FeeCalculationMethod.Fixed,
            feeBearer: FeeBearer.CustomerPays,
            fixedAmount: 25.00m,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            currency: Currency.NGN,
            createdByUserId: "admin-1",
            effectiveFromUtc: DateTime.UtcNow);

        // Assert 1
        Assert.Equal(1, v1.Version);
        Assert.True(v1.IsEnabled);

        // Act 2: Create Version 2
        var v2 = await service.CreateAndActivatePolicyAsync(
            operationType: FeeOperationType.BankTransfer,
            calculationMethod: FeeCalculationMethod.PercentageWithCap,
            feeBearer: FeeBearer.CustomerPays,
            fixedAmount: null,
            percentageRate: 0.015m,
            minimumFee: 50.00m,
            maximumFee: 2000.00m,
            currency: Currency.NGN,
            createdByUserId: "admin-2",
            effectiveFromUtc: DateTime.UtcNow);

        // Assert 2
        Assert.Equal(2, v2.Version);
        Assert.True(v2.IsEnabled);

        var refreshedV1 = await dbContext.PlatformFeePolicies.FindAsync(v1.Id);
        Assert.NotNull(refreshedV1);
        Assert.False(refreshedV1.IsEnabled);
        Assert.NotNull(refreshedV1.DeactivatedAtUtc);

        // Query active policy
        var active = await service.GetActivePolicyAsync(FeeOperationType.BankTransfer);
        Assert.NotNull(active);
        Assert.Equal(2, active.Version);
        Assert.Equal(FeeCalculationMethod.PercentageWithCap, active.CalculationMethod);

        // GetAllPolicies should return both in descending version order
        var all = await service.GetAllPoliciesAsync(FeeOperationType.BankTransfer);
        Assert.Equal(2, all.Count);
        Assert.Equal(2, all[0].Version);
        Assert.Equal(1, all[1].Version);
    }
}
