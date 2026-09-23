using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFundingTransactionSenderDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SenderAccountName",
                table: "FundingTransactions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderAccountNumber",
                table: "FundingTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderBankCode",
                table: "FundingTransactions",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderBankName",
                table: "FundingTransactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderAccountName",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "SenderAccountNumber",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "SenderBankCode",
                table: "FundingTransactions");

            migrationBuilder.DropColumn(
                name: "SenderBankName",
                table: "FundingTransactions");
        }
    }
}
