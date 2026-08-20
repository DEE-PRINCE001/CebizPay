using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2CBankTransferAndFeePolicy : Migration
    {
        private static readonly string[] IsEnabledEffectiveFromColumns = ["IsEnabled", "EffectiveFrom"];
        private static readonly string[] DestinationBankAndAccountNumberColumns = ["DestinationBankCode", "DestinationAccountNumber"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankTransferFeePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    PercentageRate = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MinimumFee = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MaximumFee = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransferFeePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderWalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBankCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationAccountNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DestinationAccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    FeeAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalDebited = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    FeePolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    FeePolicyVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankTransfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferFeePolicies_IsEnabled_EffectiveFrom",
                table: "BankTransferFeePolicies",
                columns: IsEnabledEffectiveFromColumns);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransferFeePolicies_Version",
                table: "BankTransferFeePolicies",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_CreatedAtUtc",
                table: "BankTransfers",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_DestinationBank_AccountNumber",
                table: "BankTransfers",
                columns: DestinationBankAndAccountNumberColumns);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_LedgerTransactionId",
                table: "BankTransfers",
                column: "LedgerTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_Reference",
                table: "BankTransfers",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_SenderWalletId",
                table: "BankTransfers",
                column: "SenderWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_BankTransfers_Status",
                table: "BankTransfers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankTransferFeePolicies");

            migrationBuilder.DropTable(
                name: "BankTransfers");
        }
    }
}
