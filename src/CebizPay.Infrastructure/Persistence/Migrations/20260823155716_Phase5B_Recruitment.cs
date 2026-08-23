using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861, CA1707

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5B_Recruitment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId",
                table: "OrganizationMemberships");

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalaryLevelId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkforceRoleId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CorporateLoanPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    MinimumDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    MaximumDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    RepaymentFrequency = table.Column<int>(type: "integer", nullable: false),
                    MinimumMonthlySalary = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateLoanPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkforceRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalaryLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmploymentType = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Responsibilities = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApplicationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPostings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPostings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobPostings_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobPostings_SalaryLevels_SalaryLevelId",
                        column: x => x.SalaryLevelId,
                        principalTable: "SalaryLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobPostings_WorkforceRoles_WorkforceRoleId",
                        column: x => x.WorkforceRoleId,
                        principalTable: "WorkforceRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LoanApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ApplicantName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRateSnapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    RepaymentFrequency = table.Column<int>(type: "integer", nullable: false),
                    ComputedMonthlyPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComputedTotalInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ComputedTotalRepayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    VerifiedSalarySnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExistingMonthlyDebtSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedMonthlyPaymentSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalMonthlyDebtSnapshot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DebtToIncomeRatioSnapshot = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    IsDtiCompliantSnapshot = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UnderwritingReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeclinedReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeciderUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoanContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LoanApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BorrowerUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BorrowerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LoanType = table.Column<int>(type: "integer", nullable: false),
                    OriginalPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    TotalInterest = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRepayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RepaymentFrequency = table.Column<int>(type: "integer", nullable: false),
                    NumberOfInstallments = table.Column<int>(type: "integer", nullable: false),
                    MonthlyInstallmentAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OutstandingPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmountPaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedEndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DisbursementLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisbursedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConvertedToContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedFromContractId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConversionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanContracts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayrollBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrossPay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Deductions = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BankName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentVouchers_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentVouchers_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayrollBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchReference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    SelectionMode = table.Column<int>(type: "integer", nullable: false),
                    SelectionCriteriaJson = table.Column<string>(type: "text", nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TotalEmployees = table.Column<int>(type: "integer", nullable: false),
                    TotalGrossAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalDeductionsAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalNetAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollBatches_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavingsAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavingsPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrincipalBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AccruedInterest = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalInterestWithdrawn = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    InterestRateSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InterestPolicyVersionSnapshot = table.Column<int>(type: "integer", nullable: false),
                    PenaltyRateSnapshot = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContributionFrequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaturityDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaturedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WithdrawalLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    EarlyWithdrawalPenaltyAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ForfeitedInterestAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsInterestPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnnualRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsInterestPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SavingsPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OwnerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlanType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumDurationDays = table.Column<int>(type: "integer", nullable: false),
                    MaximumDurationDays = table.Column<int>(type: "integer", nullable: false),
                    TargetAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ContributionFrequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    InterestPolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StandardIndividualLoanPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AnnualInterestRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    RepaymentFrequency = table.Column<int>(type: "integer", nullable: false),
                    MaximumDurationMonths = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardIndividualLoanPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThriftGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContributionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalPositions = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PositionSelectionDeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CurrentCycleNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThriftPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeneficiaryUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftPayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ThriftReimbursements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NetRefundAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReimbursedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftReimbursements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VasTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Network = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProductName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VasTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecruitmentApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApplicantName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ApplicantEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ApplicantPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ResumeReference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CoverLetter = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecruitmentApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecruitmentApplications_JobPostings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalTable: "JobPostings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecruitmentApplications_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoanRepaymentScheduleItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallmentNumber = table.Column<int>(type: "integer", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ScheduledAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrincipalComponent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestComponent = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MissedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayrollItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanRepaymentScheduleItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoanRepaymentScheduleItems_LoanContracts_LoanContractId",
                        column: x => x.LoanContractId,
                        principalTable: "LoanContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EmployeeName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EmployeeEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkforceRoleId = table.Column<Guid>(type: "uuid", nullable: true),
                    SalaryLevelId = table.Column<Guid>(type: "uuid", nullable: true),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    GrossPay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalDeductions = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DeductionsDetailJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClaimedByWorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentAttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentVoucherId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastFailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LastFailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollItems_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollItems_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollItems_PayrollBatches_PayrollBatchId",
                        column: x => x.PayrollBatchId,
                        principalTable: "PayrollBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavingsContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavingsAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsContributions_SavingsAccounts_SavingsAccountId",
                        column: x => x.SavingsAccountId,
                        principalTable: "SavingsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavingsInterestAccruals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavingsAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccrualDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrincipalBasis = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PolicyVersion = table.Column<int>(type: "integer", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavingsInterestAccruals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavingsInterestAccruals_SavingsAccounts_SavingsAccountId",
                        column: x => x.SavingsAccountId,
                        principalTable: "SavingsAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThriftCycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleNumber = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TargetPayoutPosition = table.Column<int>(type: "integer", nullable: false),
                    TargetBeneficiaryUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TotalExpectedPool = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCollectedPool = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayoutCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayoutLedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftCycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThriftCycles_ThriftGroups_ThriftGroupId",
                        column: x => x.ThriftGroupId,
                        principalTable: "ThriftGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThriftInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    InvitationCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AcceptedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThriftInvitations_ThriftGroups_ThriftGroupId",
                        column: x => x.ThriftGroupId,
                        principalTable: "ThriftGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThriftMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConsecutiveMissedCycles = table.Column<int>(type: "integer", nullable: false),
                    TotalContributed = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalPayoutReceived = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PositionSelectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThriftMembers_ThriftGroups_ThriftGroupId",
                        column: x => x.ThriftGroupId,
                        principalTable: "ThriftGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PayrollExecutionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollExecutionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollExecutionAttempts_PayrollItems_PayrollItemId",
                        column: x => x.PayrollItemId,
                        principalTable: "PayrollItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThriftContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentAttemptId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThriftContributions_ThriftCycles_ThriftCycleId",
                        column: x => x.ThriftCycleId,
                        principalTable: "ThriftCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_DepartmentId",
                table: "OrganizationMemberships",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_DepartmentId",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_SalaryLevelId",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "SalaryLevelId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_WorkforceRoleId",
                table: "OrganizationMemberships",
                columns: new[] { "OrganizationId", "WorkforceRoleId" });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_SalaryLevelId",
                table: "OrganizationMemberships",
                column: "SalaryLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_WorkforceRoleId",
                table: "OrganizationMemberships",
                column: "WorkforceRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateLoanPlans_OrganizationId_IsActive",
                table: "CorporateLoanPlans",
                columns: new[] { "OrganizationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateLoanPlans_OrganizationId_Name",
                table: "CorporateLoanPlans",
                columns: new[] { "OrganizationId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_DepartmentId",
                table: "JobPostings",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_OrganizationId_Status",
                table: "JobPostings",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_SalaryLevelId",
                table: "JobPostings",
                column: "SalaryLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_Status_ApplicationDeadline",
                table: "JobPostings",
                columns: new[] { "Status", "ApplicationDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_JobPostings_WorkforceRoleId",
                table: "JobPostings",
                column: "WorkforceRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_ApplicantUserId_Status",
                table: "LoanApplications",
                columns: new[] { "ApplicantUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_ApplicationReference",
                table: "LoanApplications",
                column: "ApplicationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_OrganizationId_Status",
                table: "LoanApplications",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_BorrowerUserId_Status",
                table: "LoanContracts",
                columns: new[] { "BorrowerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_ContractReference",
                table: "LoanContracts",
                column: "ContractReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_OrganizationId_BorrowerUserId_LoanType_Status",
                table: "LoanContracts",
                columns: new[] { "OrganizationId", "BorrowerUserId", "LoanType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_OrganizationId_Status",
                table: "LoanContracts",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_DueDate_Status",
                table: "LoanRepaymentScheduleItems",
                columns: new[] { "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_LoanContractId_InstallmentNumber",
                table: "LoanRepaymentScheduleItems",
                columns: new[] { "LoanContractId", "InstallmentNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_LoanContractId_Status",
                table: "LoanRepaymentScheduleItems",
                columns: new[] { "LoanContractId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_LedgerTransactionId",
                table: "PaymentVouchers",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_OrganizationId_CreatedAtUtc",
                table: "PaymentVouchers",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_PayrollBatchId",
                table: "PaymentVouchers",
                column: "PayrollBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_PayrollItemId",
                table: "PaymentVouchers",
                column: "PayrollItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_VoucherReference",
                table: "PaymentVouchers",
                column: "VoucherReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_BatchReference",
                table: "PayrollBatches",
                column: "BatchReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_OrganizationId_CreatedAtUtc",
                table: "PayrollBatches",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_OrganizationId_Status",
                table: "PayrollBatches",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollExecutionAttempts_PayrollItemId_AttemptNumber",
                table: "PayrollExecutionAttempts",
                columns: new[] { "PayrollItemId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_LedgerTransactionId",
                table: "PayrollItems",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_OrganizationId_Status",
                table: "PayrollItems",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollBatchId_EmployeeUserId",
                table: "PayrollItems",
                columns: new[] { "PayrollBatchId", "EmployeeUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollBatchId_Status",
                table: "PayrollItems",
                columns: new[] { "PayrollBatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_Status_ClaimedAtUtc",
                table: "PayrollItems",
                columns: new[] { "Status", "ClaimedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentApplications_ApplicantUserId",
                table: "RecruitmentApplications",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentApplications_JobPostingId_ApplicantEmail",
                table: "RecruitmentApplications",
                columns: new[] { "JobPostingId", "ApplicantEmail" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentApplications_JobPostingId_Status",
                table: "RecruitmentApplications",
                columns: new[] { "JobPostingId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentApplications_OrganizationId_Status",
                table: "RecruitmentApplications",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_OrganizationId",
                table: "SavingsAccounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_OwnerUserId",
                table: "SavingsAccounts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_SavingsPlanId",
                table: "SavingsAccounts",
                column: "SavingsPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_Status",
                table: "SavingsAccounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsContributions_IdempotencyKey",
                table: "SavingsContributions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsContributions_SavingsAccountId",
                table: "SavingsContributions",
                column: "SavingsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestAccruals_SavingsAccountId_AccrualDate",
                table: "SavingsInterestAccruals",
                columns: new[] { "SavingsAccountId", "AccrualDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestPolicies_PlanType_IsActive",
                table: "SavingsInterestPolicies",
                columns: new[] { "PlanType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestPolicies_PlanType_Version",
                table: "SavingsInterestPolicies",
                columns: new[] { "PlanType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlans_OrganizationId",
                table: "SavingsPlans",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlans_PlanType_IsActive",
                table: "SavingsPlans",
                columns: new[] { "PlanType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ThriftContributions_IdempotencyKey",
                table: "ThriftContributions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftContributions_ThriftCycleId_MemberId",
                table: "ThriftContributions",
                columns: new[] { "ThriftCycleId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftContributions_ThriftGroupId",
                table: "ThriftContributions",
                column: "ThriftGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftContributions_UserId",
                table: "ThriftContributions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_DueDateUtc",
                table: "ThriftCycles",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_Status",
                table: "ThriftCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_ThriftGroupId_CycleNumber",
                table: "ThriftCycles",
                columns: new[] { "ThriftGroupId", "CycleNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftGroups_CreatorUserId",
                table: "ThriftGroups",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftGroups_OrganizationId",
                table: "ThriftGroups",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftGroups_Status",
                table: "ThriftGroups",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftInvitations_InvitationCode",
                table: "ThriftInvitations",
                column: "InvitationCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftInvitations_ThriftGroupId",
                table: "ThriftInvitations",
                column: "ThriftGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftMembers_ThriftGroupId_Position",
                table: "ThriftMembers",
                columns: new[] { "ThriftGroupId", "Position" },
                unique: true,
                filter: "\"Position\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftMembers_ThriftGroupId_UserId",
                table: "ThriftMembers",
                columns: new[] { "ThriftGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftPayouts_BeneficiaryUserId",
                table: "ThriftPayouts",
                column: "BeneficiaryUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftPayouts_IdempotencyKey",
                table: "ThriftPayouts",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftPayouts_ThriftCycleId",
                table: "ThriftPayouts",
                column: "ThriftCycleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftPayouts_ThriftGroupId",
                table: "ThriftPayouts",
                column: "ThriftGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftReimbursements_IdempotencyKey",
                table: "ThriftReimbursements",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftReimbursements_MemberId",
                table: "ThriftReimbursements",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftReimbursements_ThriftGroupId",
                table: "ThriftReimbursements",
                column: "ThriftGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftReimbursements_UserId",
                table: "ThriftReimbursements",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VasTransactions_CreatedAtUtc",
                table: "VasTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VasTransactions_OrganizationId",
                table: "VasTransactions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VasTransactions_Reference",
                table: "VasTransactions",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VasTransactions_Status",
                table: "VasTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_VasTransactions_UserId",
                table: "VasTransactions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_Departments_DepartmentId",
                table: "OrganizationMemberships",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_SalaryLevels_SalaryLevelId",
                table: "OrganizationMemberships",
                column: "SalaryLevelId",
                principalTable: "SalaryLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_WorkforceRoles_WorkforceRoleId",
                table: "OrganizationMemberships",
                column: "WorkforceRoleId",
                principalTable: "WorkforceRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_Departments_DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_SalaryLevels_SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_WorkforceRoles_WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropTable(
                name: "CorporateLoanPlans");

            migrationBuilder.DropTable(
                name: "LoanApplications");

            migrationBuilder.DropTable(
                name: "LoanRepaymentScheduleItems");

            migrationBuilder.DropTable(
                name: "PaymentVouchers");

            migrationBuilder.DropTable(
                name: "PayrollExecutionAttempts");

            migrationBuilder.DropTable(
                name: "RecruitmentApplications");

            migrationBuilder.DropTable(
                name: "SavingsContributions");

            migrationBuilder.DropTable(
                name: "SavingsInterestAccruals");

            migrationBuilder.DropTable(
                name: "SavingsInterestPolicies");

            migrationBuilder.DropTable(
                name: "SavingsPlans");

            migrationBuilder.DropTable(
                name: "StandardIndividualLoanPolicies");

            migrationBuilder.DropTable(
                name: "ThriftContributions");

            migrationBuilder.DropTable(
                name: "ThriftInvitations");

            migrationBuilder.DropTable(
                name: "ThriftMembers");

            migrationBuilder.DropTable(
                name: "ThriftPayouts");

            migrationBuilder.DropTable(
                name: "ThriftReimbursements");

            migrationBuilder.DropTable(
                name: "VasTransactions");

            migrationBuilder.DropTable(
                name: "LoanContracts");

            migrationBuilder.DropTable(
                name: "PayrollItems");

            migrationBuilder.DropTable(
                name: "JobPostings");

            migrationBuilder.DropTable(
                name: "SavingsAccounts");

            migrationBuilder.DropTable(
                name: "ThriftCycles");

            migrationBuilder.DropTable(
                name: "PayrollBatches");

            migrationBuilder.DropTable(
                name: "ThriftGroups");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId",
                table: "OrganizationMemberships",
                column: "OrganizationId");
        }
    }
}
