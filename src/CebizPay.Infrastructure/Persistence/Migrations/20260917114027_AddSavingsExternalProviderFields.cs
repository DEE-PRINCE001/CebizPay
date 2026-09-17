using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavingsExternalProviderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalCustomerId",
                table: "SavingsAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalPlanId",
                table: "SavingsAccounts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatus",
                table: "SavingsAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastYieldSyncAtUtc",
                table: "SavingsAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "SavingsAccounts",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SavingsAccounts_ExternalPlanId",
                table: "SavingsAccounts",
                column: "ExternalPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SavingsAccounts_ExternalPlanId",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalCustomerId",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalPlanId",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "ExternalStatus",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "LastYieldSyncAtUtc",
                table: "SavingsAccounts");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "SavingsAccounts");
        }
    }
}
