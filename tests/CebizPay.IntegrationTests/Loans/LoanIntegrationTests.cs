using CebizPay.Application.Common.Interfaces.Loans;
using CebizPay.Application.Common.Interfaces.Payroll;
using CebizPay.Domain.Entities;
using CebizPay.Domain.Enums;
using CebizPay.Domain.Finance.Entities;
using CebizPay.Domain.Finance.Enums;
using CebizPay.Domain.Loans.Entities;
using CebizPay.Domain.Loans.Enums;
using CebizPay.Domain.Payroll.Entities;
using CebizPay.Domain.Payroll.Enums;
using CebizPay.Infrastructure.Finance;
using CebizPay.Infrastructure.Loans;
using CebizPay.Infrastructure.Payroll;
using CebizPay.Infrastructure.Persistence;
using CebizPay.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CebizPay.IntegrationTests.Loans;

/// <summary>
/// PostgreSQL Testcontainers integration tests verifying end-to-end Corporate Payroll Loan lifecycle:
/// product setup, DTI underwriting, submission, self-approval prevention, atomic ledger disbursement,
/// payroll auto-deductions and installment marking, and offboarding individual loan conversion.
/// </summary>
public sealed class LoanIntegrationTests : IClassFixture<InfrastructureFixture>
{
    private readonly InfrastructureFixture _fixture;

    public LoanIntegrationTests(InfrastructureFixture fixture)
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
    public async Task CompleteCorporateLoanLifecycle_EndToEnd_ShouldSettleDisbursementPayrollDeductionAndOffboardConversion()
    {
        // 1. Arrange Services
        await using var dbContext = await CreateDbContextAsync();
        var outboxService = new OutboxService(dbContext);
        var ledgerPostingService = new LedgerPostingService(dbContext);
        var calcService = new LoanCalculationService();
        var underwritingService = new LoanUnderwritingService(dbContext, calcService, NullLogger<LoanUnderwritingService>.Instance);
        var planService = new LoanPlanService(dbContext, outboxService, NullLogger<LoanPlanService>.Instance);
        var appService = new LoanApplicationService(dbContext, calcService, underwritingService, ledgerPostingService, outboxService, NullLogger<LoanApplicationService>.Instance);
        var contractService = new LoanContractService(dbContext, outboxService, NullLogger<LoanContractService>.Instance);

        var deductionProvider = new PayrollLoanDeductionProvider(dbContext);
        var payrollCalcService = new PayrollCalculationService(dbContext, deductionProvider);
        var payrollBatchService = new PayrollBatchService(dbContext, payrollCalcService, outboxService, NullLogger<PayrollBatchService>.Instance);
        var payrollExecService = new PayrollExecutionService(dbContext, ledgerPostingService, outboxService, NullLogger<PayrollExecutionService>.Instance);

        // 2. Setup Organization, Org Wallet, Admin, and Employee
        var org = new Organization($"Credit Corp {Guid.NewGuid():N}", "credit@corp.com", "+2348011111111");
        org.TransitionStatus(OrganizationStatus.Verified);
        dbContext.Organizations.Add(org);

        var orgWallet = Wallet.CreateOrganizationWallet(org.Id, Currency.NGN);
        orgWallet.Credit(10_000_000m); // Fund org wallet for payroll
        dbContext.Wallets.Add(orgWallet);

        var adminUserId = $"admin-{Guid.NewGuid():N}";
        var adminMembership = new OrganizationMembership(adminUserId, org.Id, MembershipRoleType.Admin);
        dbContext.OrganizationMemberships.Add(adminMembership);

        var staffUserId = $"staff-{Guid.NewGuid():N}";
        var salaryLevel = new SalaryLevel(org.Id, "Principal Engineer", 1_000_000m, "NGN");
        dbContext.SalaryLevels.Add(salaryLevel);

        var staffMembership = new OrganizationMembership(staffUserId, org.Id, MembershipRoleType.Member, null, null, salaryLevel.Id);
        dbContext.OrganizationMemberships.Add(staffMembership);

        var staffWallet = Wallet.CreateIndividualWallet(staffUserId, Currency.NGN);
        dbContext.Wallets.Add(staffWallet);

        await dbContext.SaveChangesAsync();

        // 3. Create Corporate Loan Plan
        var createPlanReq = new CreateLoanPlanRequest(
            Name: "Executive Staff Loan",
            Description: "Low interest 12-month staff loan",
            MinimumAmount: 100_000m,
            MaximumAmount: 3_000_000m,
            InterestRate: 0.10m,
            MinimumDurationMonths: 6,
            MaximumDurationMonths: 24,
            MinimumMonthlySalary: 500_000m,
            RepaymentFrequency: RepaymentFrequency.Monthly);

        var planDto = await planService.CreatePlanAsync(org.Id, createPlanReq, adminUserId);
        Assert.NotNull(planDto);
        Assert.True(planDto.IsActive);

        // 4. Request Calculation Preview
        var previewReq = new LoanCalculationPreviewRequest(planDto.Id, 1_200_000m, 12);
        var preview = await appService.PreviewApplicationAsync(org.Id, staffUserId, previewReq);
        Assert.True(preview.IsEligible);
        Assert.True(preview.IsDtiCompliant);
        Assert.Equal(110_000m, preview.MonthlyPayment);
        Assert.Equal(120_000m, preview.TotalInterest);
        Assert.Equal(1_320_000m, preview.TotalRepayment);

        // 5. Submit Loan Application
        var submitReq = new SubmitLoanApplicationRequest(planDto.Id, 1_200_000m, 12);
        var appDto = await appService.SubmitApplicationAsync(org.Id, staffUserId, submitReq);
        Assert.Equal(LoanApplicationStatus.Submitted, appDto.Status);

        // 6. Verify Self-Approval Prevention
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            appService.ApproveApplicationAsync(org.Id, appDto.Id, staffUserId));

        // 7. Approve Loan Application by Admin (Disbursement flow)
        var contractDto = await appService.ApproveApplicationAsync(org.Id, appDto.Id, adminUserId);
        Assert.NotNull(contractDto);
        Assert.Equal(LoanContractStatus.Active, contractDto.Status);
        Assert.Equal(12, contractDto.RepaymentSchedule.Count);
        Assert.NotNull(contractDto.DisbursementLedgerTransactionId);

        // Verify staff wallet balance credited with principal (1,200,000 NGN)
        var refreshedStaffWallet = await dbContext.Wallets.AsNoTracking().FirstAsync(w => w.Id == staffWallet.Id);
        Assert.Equal(1_200_000m, refreshedStaffWallet.AvailableBalance);

        // 8. Run Payroll Batch & Auto-Deduct Repayment
        var criteria = new PayrollSelectionCriteria(
            Mode: PayrollSelectionMode.Individual,
            EmployeeUserIds: [staffUserId]);

        var batchDto = await payrollBatchService.CreateAndEnqueueBatchAsync(
            org.Id,
            adminUserId,
            Currency.NGN,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            criteria);
        Assert.Equal(1, batchDto.TotalEmployees);

        // Execute payroll batch item
        var payrollItem = await dbContext.PayrollItems.FirstAsync(i => i.PayrollBatchId == batchDto.BatchId);
        var execResult = await payrollExecService.ExecutePayrollItemAsync(payrollItem.Id, "test-worker-id");
        Assert.True(execResult.Succeeded);

        // Verify payroll deduction included loan installment (110,000 NGN)
        var refreshedItem = await dbContext.PayrollItems.AsNoTracking().FirstAsync(i => i.Id == payrollItem.Id);
        Assert.Equal(1_000_000m, refreshedItem.GrossPay);
        Assert.Equal(110_000m, refreshedItem.TotalDeductions);
        Assert.Equal(890_000m, refreshedItem.NetPay);

        // Verify staff wallet received NetPay (1,200,000 previous balance + 890,000 net pay = 2,090,000 NGN)
        refreshedStaffWallet = await dbContext.Wallets.AsNoTracking().FirstAsync(w => w.Id == staffWallet.Id);
        Assert.Equal(2_090_000m, refreshedStaffWallet.AvailableBalance);

        // Verify Loan Installment #1 marked Paid and Outstanding Principal reduced
        var refreshedContract = await dbContext.LoanContracts.Include(c => c.RepaymentSchedule).AsNoTracking().FirstAsync(c => c.Id == contractDto.Id);
        Assert.Equal(1_100_000m, refreshedContract.OutstandingPrincipal);
        Assert.Equal(110_000m, refreshedContract.TotalAmountPaid);
        var installment1 = refreshedContract.RepaymentSchedule.First(i => i.InstallmentNumber == 1);
        Assert.Equal(LoanRepaymentStatus.Paid, installment1.Status);
        Assert.NotNull(installment1.PayrollItemId);

        // 9. Staff Offboarding: Convert Corporate Loan to Individual Loan
        var convertedList = await contractService.ConvertTerminatedStaffLoansAsync(
            org.Id, staffUserId, "Employee resigned from company.", adminUserId);

        Assert.Single(convertedList);
        var convertedContractDto = convertedList[0];
        Assert.Equal(LoanType.StandardIndividualLoan, convertedContractDto.LoanType);
        Assert.Equal(LoanContractStatus.Active, convertedContractDto.Status);
        Assert.Equal(1_100_000m, convertedContractDto.OriginalPrincipal);
        Assert.Equal(1_210_000m, convertedContractDto.TotalRepayment); // 11 remaining installments * 110k
        Assert.Equal(11, convertedContractDto.NumberOfInstallments);

        // Verify original loan is now ConvertedToIndividual
        refreshedContract = await dbContext.LoanContracts.AsNoTracking().FirstAsync(c => c.Id == contractDto.Id);
        Assert.Equal(LoanContractStatus.ConvertedToIndividual, refreshedContract.Status);
        Assert.Equal(convertedContractDto.Id, refreshedContract.ConvertedToContractId);

        // 10. Subsequent Payroll Run: Verify No Deductions for Converted Loan
        var nextDeductions = await deductionProvider.GetDeductionsForEmployeeAsync(
            org.Id, staffUserId, 1_000_000m, Currency.NGN);
        Assert.Empty(nextDeductions);
    }
}
