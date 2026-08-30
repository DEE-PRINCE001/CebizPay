#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCardFundingSavedCardsAndRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderCustomerReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderToken = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    Brand = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiryMonth = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    ExpiryYear = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CardHolderName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardRefunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundingTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    RefundReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderRefundReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardRefunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CardVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SavedCardId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false, defaultValue: 0m),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CardVerifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedCards_UserId_Status",
                table: "SavedCards",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedCards_WalletId",
                table: "SavedCards",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedCards_UserId_Provider_ProviderToken",
                table: "SavedCards",
                columns: new[] { "UserId", "Provider", "ProviderToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardRefunds_RefundReference",
                table: "CardRefunds",
                column: "RefundReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardRefunds_IdempotencyKey",
                table: "CardRefunds",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardRefunds_FundingTransactionId",
                table: "CardRefunds",
                column: "FundingTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CardRefunds_WalletId",
                table: "CardRefunds",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_CardVerifications_Reference",
                table: "CardVerifications",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CardVerifications_UserId_Status",
                table: "CardVerifications",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CardVerifications");
            migrationBuilder.DropTable(name: "CardRefunds");
            migrationBuilder.DropTable(name: "SavedCards");
        }
    }
}
