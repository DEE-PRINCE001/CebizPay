using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4CSavingsAndThrift : Migration
    {
        private static readonly string[] PolicyPlanVersionColumns = ["PlanType", "Version"];
        private static readonly string[] PolicyPlanActiveColumns = ["PlanType", "IsActive"];
        private static readonly string[] PlanTypeActiveColumns = ["PlanType", "IsActive"];
        private static readonly string[] AccrualAccountDateColumns = ["SavingsAccountId", "AccrualDate"];
        private static readonly string[] MemberGroupUserColumns = ["ThriftGroupId", "UserId"];
        private static readonly string[] MemberGroupPositionColumns = ["ThriftGroupId", "Position"];
        private static readonly string[] CycleGroupNumberColumns = ["ThriftGroupId", "CycleNumber"];
        private static readonly string[] ContributionCycleMemberColumns = ["ThriftCycleId", "MemberId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // Indexes
            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestPolicies_PlanType_Version",
                table: "SavingsInterestPolicies",
                columns: PolicyPlanVersionColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestPolicies_PlanType_IsActive",
                table: "SavingsInterestPolicies",
                columns: PolicyPlanActiveColumns);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlans_OrganizationId",
                table: "SavingsPlans",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsPlans_PlanType_IsActive",
                table: "SavingsPlans",
                columns: PlanTypeActiveColumns);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_SavingsPlanId",
                table: "SavingsAccounts",
                column: "SavingsPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_OwnerUserId",
                table: "SavingsAccounts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_OrganizationId",
                table: "SavingsAccounts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_Status",
                table: "SavingsAccounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsContributions_SavingsAccountId",
                table: "SavingsContributions",
                column: "SavingsAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsContributions_IdempotencyKey",
                table: "SavingsContributions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_SavingsInterestAccruals_SavingsAccountId_AccrualDate",
                table: "SavingsInterestAccruals",
                columns: AccrualAccountDateColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftGroups_OrganizationId",
                table: "ThriftGroups",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftGroups_CreatorUserId",
                table: "ThriftGroups",
                column: "CreatorUserId");

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
                name: "IX_ThriftMembers_ThriftGroupId_UserId",
                table: "ThriftMembers",
                columns: MemberGroupUserColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftMembers_ThriftGroupId_Position",
                table: "ThriftMembers",
                columns: MemberGroupPositionColumns,
                unique: true,
                filter: "\"Position\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_ThriftGroupId_CycleNumber",
                table: "ThriftCycles",
                columns: CycleGroupNumberColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_Status",
                table: "ThriftCycles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftCycles_DueDateUtc",
                table: "ThriftCycles",
                column: "DueDateUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ThriftContributions_ThriftCycleId_MemberId",
                table: "ThriftContributions",
                columns: ContributionCycleMemberColumns,
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
                name: "IX_ThriftContributions_IdempotencyKey",
                table: "ThriftContributions",
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
                name: "IX_ThriftPayouts_BeneficiaryUserId",
                table: "ThriftPayouts",
                column: "BeneficiaryUserId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SavingsContributions");
            migrationBuilder.DropTable(name: "SavingsInterestAccruals");
            migrationBuilder.DropTable(name: "SavingsAccounts");
            migrationBuilder.DropTable(name: "SavingsPlans");
            migrationBuilder.DropTable(name: "SavingsInterestPolicies");
            migrationBuilder.DropTable(name: "ThriftContributions");
            migrationBuilder.DropTable(name: "ThriftCycles");
            migrationBuilder.DropTable(name: "ThriftInvitations");
            migrationBuilder.DropTable(name: "ThriftMembers");
            migrationBuilder.DropTable(name: "ThriftPayouts");
            migrationBuilder.DropTable(name: "ThriftReimbursements");
            migrationBuilder.DropTable(name: "ThriftGroups");
        }
    }
}
