using CebizPay.Application.Common.Interfaces.Messaging;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CebizPay.IntegrationTests.Finance;

/// <summary>
/// PostgreSQL Testcontainers integration tests for <see cref="PlatformFeePolicy"/> and <see cref="PlatformFeePolicyService"/>.
/// Validates PostgreSQL partial unique index constraints and policy versioning lifecycle.
/// </summary>
public sealed class PlatformFeePolicyIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;
    private readonly IOutboxService _outbox = Substitute.For<IOutboxService>();

    public PlatformFeePolicyIntegrationTests(InfrastructureFixture fixture)
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

    [Fact]
    public async Task DatabaseConstraint_MultipleActivePoliciesOnSameOperation_ShouldThrowDbUpdateException()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();

        var p1 = PlatformFeePolicy.CreateFixed(
            operationType: FeeOperationType.VirtualAccountFunding,
            fixedAmount: 50m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 1,
            createdByUserId: "admin-1");

        dbContext.PlatformFeePolicies.Add(p1);
        await dbContext.SaveChangesAsync();

        // Act & Assert: Attempt to insert a second active policy for the same operation type directly
        var p2 = PlatformFeePolicy.CreatePercentage(
            operationType: FeeOperationType.VirtualAccountFunding,
            percentageRate: 0.01m,
            feeBearer: FeeBearer.CustomerPays,
            currency: Currency.NGN,
            version: 2,
            createdByUserId: "admin-2");

        dbContext.PlatformFeePolicies.Add(p2);

        // PostgreSQL partial unique index IX_PlatformFeePolicies_OperationType_Active must reject this
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task CreateAndActivatePolicyAsync_SequentialVersions_ShouldPersistCorrectlyInPostgres()
    {
        // Arrange
        await using var dbContext = await CreateDbContextAsync();
        var service = new PlatformFeePolicyService(dbContext, _outbox, NullLogger<PlatformFeePolicyService>.Instance);

        // Act
        var v1 = await service.CreateAndActivatePolicyAsync(
            operationType: FeeOperationType.CardFunding,
            calculationMethod: FeeCalculationMethod.Fixed,
            feeBearer: FeeBearer.CustomerPays,
            fixedAmount: 100m,
            percentageRate: null,
            minimumFee: null,
            maximumFee: null,
            currency: Currency.NGN,
            createdByUserId: "admin-1",
            effectiveFromUtc: DateTime.UtcNow);

        var v2 = await service.CreateAndActivatePolicyAsync(
            operationType: FeeOperationType.CardFunding,
            calculationMethod: FeeCalculationMethod.Percentage,
            feeBearer: FeeBearer.DeductFromFunds,
            fixedAmount: null,
            percentageRate: 0.015m,
            minimumFee: null,
            maximumFee: null,
            currency: Currency.NGN,
            createdByUserId: "admin-2",
            effectiveFromUtc: DateTime.UtcNow);

        // Assert
        var active = await service.GetActivePolicyAsync(FeeOperationType.CardFunding);
        Assert.NotNull(active);
        Assert.Equal(2, active.Version);
        Assert.Equal(FeeCalculationMethod.Percentage, active.CalculationMethod);
        Assert.Equal(FeeBearer.DeductFromFunds, active.FeeBearer);

        var all = await service.GetAllPoliciesAsync(FeeOperationType.CardFunding);
        Assert.Equal(2, all.Count);
    }
}
