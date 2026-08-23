using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Loans;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.IntegrationTests.Staff;

public sealed class StaffLifecycleIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public StaffLifecycleIntegrationTests(InfrastructureFixture fixture)
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
    public async Task StaffLifecycle_DirectCreation_Assignment_And_Termination_With_LoanConversion()
    {
        await using var dbContext = await CreateDbContextAsync();

        var adminUserId = $"admin_{Guid.NewGuid():N}";
        var org = new Organization("MegaCorp", $"mega_{Guid.NewGuid():N}@test.com", "+2348000000030");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var dept = new Department(org.Id, "Technology", "Tech Dept");
        var role = new WorkforceRole(org.Id, "Software Engineer", dept.Id, "Core Dev");
        var level = new SalaryLevel(org.Id, "Principal Dev", 1_000_000m, "NGN");
        dbContext.Departments.Add(dept);
        dbContext.WorkforceRoles.Add(role);
        dbContext.SalaryLevels.Add(level);

        var staffUserId = $"staff_user_{Guid.NewGuid():N}";
        var profile = new IndividualProfile(staffUserId, "John", "Doe");
        profile.SetKycStatus(KycStatus.Verified);
        profile.UpdateProfessionalStatus(ProfessionalStatus.Staff);
        dbContext.IndividualProfiles.Add(profile);

        var membership = new OrganizationMembership(staffUserId, org.Id, MembershipRoleType.Member, dept.Id, role.Id, level.Id);
        dbContext.OrganizationMemberships.Add(membership);

        var staffWallet = Wallet.CreateIndividualWallet(staffUserId, Currency.NGN);
        dbContext.Wallets.Add(staffWallet);

        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(10_000_000m);
        dbContext.Wallets.Add(orgWallet);

        await dbContext.SaveChangesAsync();

        var outboxService = new OutboxService(dbContext);
        var ledgerPostingService = new LedgerPostingService(dbContext);
        var calcService = new LoanCalculationService();
        var underwritingService = new LoanUnderwritingService(dbContext, calcService, NullLogger<LoanUnderwritingService>.Instance);
        var planService = new LoanPlanService(dbContext, outboxService, NullLogger<LoanPlanService>.Instance);
        var appService = new LoanApplicationService(dbContext, calcService, underwritingService, ledgerPostingService, outboxService, NullLogger<LoanApplicationService>.Instance);
        var contractService = new LoanContractService(dbContext, outboxService, NullLogger<LoanContractService>.Instance);

        // 1. Verify staff is active in workforce
        var fetchedMembership = await dbContext.OrganizationMemberships.FirstAsync(m => m.Id == membership.Id);
        Assert.Equal(MembershipStatus.Active, fetchedMembership.Status);
        Assert.Equal(dept.Id, fetchedMembership.DepartmentId);
        Assert.Equal(role.Id, fetchedMembership.WorkforceRoleId);
        Assert.Equal(level.Id, fetchedMembership.SalaryLevelId);

        // 2. Create corporate loan plan and disburse loan to staff
        var planDto = await planService.CreatePlanAsync(org.Id, new CreateLoanPlanRequest(
            Name: "Staff Loan",
            Description: "Low interest",
            MinimumAmount: 100_000m,
            MaximumAmount: 1_000_000m,
            InterestRate: 0.10m,
            MinimumDurationMonths: 6,
            MaximumDurationMonths: 12,
            MinimumMonthlySalary: 500_000m,
            RepaymentFrequency: RepaymentFrequency.Monthly), adminUserId);

        var appDto = await appService.SubmitApplicationAsync(org.Id, staffUserId, new SubmitLoanApplicationRequest(planDto.Id, 500_000m, 6));
        var contractDto = await appService.ApproveApplicationAsync(org.Id, appDto.Id, adminUserId);
        Assert.NotNull(contractDto);
        Assert.Equal(LoanContractStatus.Active, contractDto.Status);

        // 3. Perform staff offboarding / termination
        var convertedList = await contractService.ConvertTerminatedStaffLoansAsync(
            org.Id, staffUserId, "Resignation", adminUserId, CancellationToken.None);

        fetchedMembership.TerminateWorkAccess("Resignation");
        profile.UpdateProfessionalStatus(ProfessionalStatus.NotAStaff);
        await dbContext.SaveChangesAsync();

        // 4. Verify corporate loan converted to individual standard loan
        Assert.Single(convertedList);
        var convertedContractDto = convertedList[0];
        Assert.Equal(LoanType.StandardIndividualLoan, convertedContractDto.LoanType);
        Assert.Equal(LoanContractStatus.Active, convertedContractDto.Status);

        // 5. Verify staff status is Terminated but Personal Profile KYC is intact
        Assert.Equal(MembershipStatus.Terminated, fetchedMembership.Status);
        Assert.Equal("Resignation", fetchedMembership.SuspensionReason);
        Assert.Equal(KycStatus.Verified, profile.KycStatus);
    }
}
