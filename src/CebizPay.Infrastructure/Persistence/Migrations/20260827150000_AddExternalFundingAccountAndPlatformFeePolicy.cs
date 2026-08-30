#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalFundingAccountAndPlatformFeePolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalFundingAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    ProviderCustomerReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderAccountReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccountNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AccountName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BankCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BankName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalFundingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalFundingAccounts_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlatformFeePolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<int>(type: "integer", nullable: false),
                    CalculationMethod = table.Column<int>(type: "integer", nullable: false),
                    FeeBearer = table.Column<int>(type: "integer", nullable: false),
                    FixedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    PercentageRate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    MinimumFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaximumFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Currency = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeactivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformFeePolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFundingAccounts_Provider_AccountNumber",
                table: "ExternalFundingAccounts",
                columns: new[] { "Provider", "AccountNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFundingAccounts_Provider_ProviderAccountReference",
                table: "ExternalFundingAccounts",
                columns: new[] { "Provider", "ProviderAccountReference" },
                unique: true,
                filter: "\"ProviderAccountReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFundingAccounts_Status",
                table: "ExternalFundingAccounts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFundingAccounts_WalletId",
                table: "ExternalFundingAccounts",
                column: "WalletId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalFundingAccounts_WalletId_IsPrimary",
                table: "ExternalFundingAccounts",
                column: "WalletId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFeePolicies_IsEnabled",
                table: "PlatformFeePolicies",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFeePolicies_OperationType_Active",
                table: "PlatformFeePolicies",
                column: "OperationType",
                unique: true,
                filter: "\"IsEnabled\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformFeePolicies_OperationType_Version",
                table: "PlatformFeePolicies",
                columns: new[] { "OperationType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalFundingAccounts");

            migrationBuilder.DropTable(
                name: "PlatformFeePolicies");
        }
    }
}
