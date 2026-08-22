using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase4APayrollAndPaymentVouchers : Migration
    {
        private static readonly string[] OrgCreatedAtColumns = ["OrganizationId", "CreatedAtUtc"];
        private static readonly string[] OrgStatusColumns = ["OrganizationId", "Status"];
        private static readonly string[] BatchEmployeeColumns = ["PayrollBatchId", "EmployeeUserId"];
        private static readonly string[] BatchStatusColumns = ["PayrollBatchId", "Status"];
        private static readonly string[] ItemAttemptColumns = ["PayrollItemId", "AttemptNumber"];
        private static readonly string[] StatusClaimedColumns = ["Status", "ClaimedAtUtc"];
        private static readonly string[] OrgDepartmentColumns = ["OrganizationId", "DepartmentId"];
        private static readonly string[] OrgRoleColumns = ["OrganizationId", "WorkforceRoleId"];
        private static readonly string[] OrgSalaryLevelColumns = ["OrganizationId", "SalaryLevelId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkforceRoleId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SalaryLevelId",
                table: "OrganizationMemberships",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_DepartmentId",
                table: "OrganizationMemberships",
                columns: OrgDepartmentColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_WorkforceRoleId",
                table: "OrganizationMemberships",
                columns: OrgRoleColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationMemberships_OrganizationId_SalaryLevelId",
                table: "OrganizationMemberships",
                columns: OrgSalaryLevelColumns);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_Departments_DepartmentId",
                table: "OrganizationMemberships",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_WorkforceRoles_WorkforceRoleId",
                table: "OrganizationMemberships",
                column: "WorkforceRoleId",
                principalTable: "WorkforceRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationMemberships_SalaryLevels_SalaryLevelId",
                table: "OrganizationMemberships",
                column: "SalaryLevelId",
                principalTable: "SalaryLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

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

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_OrganizationId_CreatedAtUtc",
                table: "PaymentVouchers",
                columns: OrgCreatedAtColumns);

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
                columns: OrgCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_OrganizationId_Status",
                table: "PayrollBatches",
                columns: OrgStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollExecutionAttempts_PayrollItemId_AttemptNumber",
                table: "PayrollExecutionAttempts",
                columns: ItemAttemptColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_LedgerTransactionId",
                table: "PayrollItems",
                column: "LedgerTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_OrganizationId_Status",
                table: "PayrollItems",
                columns: OrgStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollBatchId_EmployeeUserId",
                table: "PayrollItems",
                columns: BatchEmployeeColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_PayrollBatchId_Status",
                table: "PayrollItems",
                columns: BatchStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollItems_Status_ClaimedAtUtc",
                table: "PayrollItems",
                columns: StatusClaimedColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentVouchers");

            migrationBuilder.DropTable(
                name: "PayrollExecutionAttempts");

            migrationBuilder.DropTable(
                name: "PayrollItems");

            migrationBuilder.DropTable(
                name: "PayrollBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_Departments_DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_WorkforceRoles_WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationMemberships_SalaryLevels_SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropIndex(
                name: "IX_OrganizationMemberships_OrganizationId_SalaryLevelId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "WorkforceRoleId",
                table: "OrganizationMemberships");

            migrationBuilder.DropColumn(
                name: "SalaryLevelId",
                table: "OrganizationMemberships");
        }
    }
}
