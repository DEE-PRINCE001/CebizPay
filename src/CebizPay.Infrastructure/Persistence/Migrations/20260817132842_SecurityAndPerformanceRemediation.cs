using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments")]
    public partial class SecurityAndPerformanceRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_LedgerAccountId",
                table: "LedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Unprocessed",
                table: "OutboxMessages",
                column: "OccurredOnUtc",
                filter: "\"ProcessedOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId_CreatedAtUtc",
                table: "LedgerEntries",
                columns: new[] { "LedgerAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_AccountType_Currency",
                table: "LedgerAccounts",
                columns: new[] { "AccountType", "Currency" },
                unique: true,
                filter: "\"WalletId\" IS NULL");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_UserId_OrganizationId_Operation_Idempote~",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_UserId_Operation_IdempotencyKey",
                table: "IdempotencyRecords",
                columns: new[] { "UserId", "Operation", "IdempotencyKey" },
                unique: true,
                filter: "\"OrganizationId\" IS NULL AND \"UserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_OrganizationId_Operation_IdempotencyKey",
                table: "IdempotencyRecords",
                columns: new[] { "OrganizationId", "Operation", "IdempotencyKey" },
                unique: true,
                filter: "\"OrganizationId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Unprocessed",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_LedgerAccountId_CreatedAtUtc",
                table: "LedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_LedgerAccounts_AccountType_Currency",
                table: "LedgerAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc",
                table: "OutboxMessages",
                column: "ProcessedOnUtc",
                filter: "\"ProcessedOnUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_LedgerAccountId",
                table: "LedgerEntries",
                column: "LedgerAccountId");
        }
    }
}
