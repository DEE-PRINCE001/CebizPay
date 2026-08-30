#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonnifyAndFundingFeeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalFundingAccountId",
                table: "FundingTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderEventReference",
                table: "FundingTransactions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "FundingTransactions",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NetCreditedAmount",
                table: "FundingTransactions",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ProviderFeeAmount",
                table: "FundingTransactions",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "FeePolicyId",
                table: "FundingTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeePolicyVersion",
                table: "FundingTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FeeBearer",
                table: "FundingTransactions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransactions_ExternalFundingAccountId",
                table: "FundingTransactions",
                column: "ExternalFundingAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FundingTransactions_ExternalFundingAccountId",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "ExternalFundingAccountId",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderEventReference",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "NetCreditedAmount",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderFeeAmount",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "FeePolicyId",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "FeePolicyVersion",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "FeeBearer",
                table: "FundingTransactions");
        }
    }
}
