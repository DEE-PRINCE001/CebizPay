using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2APeerTransferAndFeePolicy : Migration
    {
        private static readonly string[] IsEnabledEffectiveFromColumns = ["IsEnabled", "EffectiveFrom"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeerTransferFeePolicies",
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
                    table.PrimaryKey("PK_PeerTransferFeePolicies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PeerTransferFeePolicies_IsEnabled_EffectiveFrom",
                table: "PeerTransferFeePolicies",
                columns: IsEnabledEffectiveFromColumns);

            migrationBuilder.CreateIndex(
                name: "IX_PeerTransferFeePolicies_Version",
                table: "PeerTransferFeePolicies",
                column: "Version",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeerTransferFeePolicies");
        }
    }
}
