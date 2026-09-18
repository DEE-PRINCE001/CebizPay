using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndividualSuspensionPnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                table: "IndividualProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAtUtc",
                table: "IndividualProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspensionReason",
                table: "IndividualProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndividualProfiles_IsSuspended",
                table: "IndividualProfiles",
                column: "IsSuspended");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_IndividualProfiles_IsSuspended",
                table: "IndividualProfiles");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                table: "IndividualProfiles");

            migrationBuilder.DropColumn(
                name: "SuspendedAtUtc",
                table: "IndividualProfiles");

            migrationBuilder.DropColumn(
                name: "SuspensionReason",
                table: "IndividualProfiles");
        }
    }
}
