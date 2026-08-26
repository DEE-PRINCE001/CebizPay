#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5E_CompanyVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyVouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayeeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayeeDetails = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyVouchers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyVouchers_OrganizationId_CreatedAtUtc",
                table: "CompanyVouchers",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyVouchers_OrganizationId_Status",
                table: "CompanyVouchers",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyVouchers_OrganizationId_VoucherNumber",
                table: "CompanyVouchers",
                columns: new[] { "OrganizationId", "VoucherNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyVouchers");
        }
    }
}
