using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4BLoansAndRepaymentSchedules : Migration
    {
        private static readonly string[] PlanOrgIsActiveColumns = ["OrganizationId", "IsActive"];
        private static readonly string[] PlanOrgNameColumns = ["OrganizationId", "Name"];
        private static readonly string[] AppUserStatusColumns = ["ApplicantUserId", "Status"];
        private static readonly string[] AppOrgStatusColumns = ["OrganizationId", "Status"];
        private static readonly string[] ContractUserStatusColumns = ["BorrowerUserId", "Status"];
        private static readonly string[] ContractOrgUserTypeStatusColumns = ["OrganizationId", "BorrowerUserId", "LoanType", "Status"];
        private static readonly string[] ContractOrgStatusColumns = ["OrganizationId", "Status"];
        private static readonly string[] ScheduleDueDateStatusColumns = ["DueDate", "Status"];
        private static readonly string[] ScheduleContractInstallmentColumns = ["LoanContractId", "InstallmentNumber"];
        private static readonly string[] ScheduleContractStatusColumns = ["LoanContractId", "Status"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateIndex(
                name: "IX_CorporateLoanPlans_OrganizationId_IsActive",
                table: "CorporateLoanPlans",
                columns: PlanOrgIsActiveColumns);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateLoanPlans_OrganizationId_Name",
                table: "CorporateLoanPlans",
                columns: PlanOrgNameColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_ApplicantUserId_Status",
                table: "LoanApplications",
                columns: AppUserStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_ApplicationReference",
                table: "LoanApplications",
                column: "ApplicationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanApplications_OrganizationId_Status",
                table: "LoanApplications",
                columns: AppOrgStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_BorrowerUserId_Status",
                table: "LoanContracts",
                columns: ContractUserStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_ContractReference",
                table: "LoanContracts",
                column: "ContractReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_OrganizationId_BorrowerUserId_LoanType_Status",
                table: "LoanContracts",
                columns: ContractOrgUserTypeStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanContracts_OrganizationId_Status",
                table: "LoanContracts",
                columns: ContractOrgStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_DueDate_Status",
                table: "LoanRepaymentScheduleItems",
                columns: ScheduleDueDateStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_LoanContractId_InstallmentNumber",
                table: "LoanRepaymentScheduleItems",
                columns: ScheduleContractInstallmentColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanRepaymentScheduleItems_LoanContractId_Status",
                table: "LoanRepaymentScheduleItems",
                columns: ScheduleContractStatusColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanRepaymentScheduleItems");

            migrationBuilder.DropTable(
                name: "CorporateLoanPlans");

            migrationBuilder.DropTable(
                name: "LoanApplications");

            migrationBuilder.DropTable(
                name: "LoanContracts");

            migrationBuilder.DropTable(
                name: "StandardIndividualLoanPolicies");
        }
    }
}
