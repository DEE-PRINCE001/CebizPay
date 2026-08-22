using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase3FVirtualAccountsAndFunding : Migration
    {
        private static readonly string[] ProviderAndAccountNumberColumns = ["Provider", "AccountNumber"];
        private static readonly string[] IndividualIdProviderCurrencyColumns = ["IndividualId", "Provider", "Currency"];
        private static readonly string[] OrganizationIdProviderCurrencyColumns = ["OrganizationId", "Provider", "Currency"];
        private static readonly string[] ProviderAndProviderTxnRefColumns = ["Provider", "ProviderTransactionReference"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VirtualAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndividualId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BankName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FundingTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    VirtualAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderTransactionReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FundingChannel = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingTransactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAccounts_Provider_AccountNumber",
                table: "VirtualAccounts",
                columns: ProviderAndAccountNumberColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAccounts_IndividualId_Provider_Currency",
                table: "VirtualAccounts",
                columns: IndividualIdProviderCurrencyColumns,
                unique: true,
                filter: "\"IndividualId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAccounts_OrganizationId_Provider_Currency",
                table: "VirtualAccounts",
                columns: OrganizationIdProviderCurrencyColumns,
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VirtualAccounts_Status",
                table: "VirtualAccounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_Provider_ProviderTransactionReference",
                table: "FundingTransactions",
                columns: ProviderAndProviderTxnRefColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_WalletId",
                table: "FundingTransactions",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_VirtualAccountId",
                table: "FundingTransactions",
                column: "VirtualAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_Status",
                table: "FundingTransactions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_CreatedAtUtc",
                table: "FundingTransactions",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundingTransactions");

            migrationBuilder.DropTable(
                name: "VirtualAccounts");
        }
    }
}
