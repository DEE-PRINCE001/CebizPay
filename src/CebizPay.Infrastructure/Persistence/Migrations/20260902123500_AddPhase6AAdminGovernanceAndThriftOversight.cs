#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase6AAdminGovernanceAndThriftOversight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "AdminProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAtUtc",
                table: "AdminProfiles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "AdminProfiles",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminProfiles_IsDeleted_IsActive_Role",
                table: "AdminProfiles",
                columns: new[] { "IsDeleted", "IsActive", "Role" });

            migrationBuilder.CreateTable(
                name: "AdminInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    RedeemedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RedeemedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminInvitations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvitations_TokenHash",
                table: "AdminInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminInvitations_Email_Status_ExpiresAtUtc",
                table: "AdminInvitations",
                columns: new[] { "Email", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateTable(
                name: "ThriftDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThriftGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResolvedByUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThriftDisputes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThriftDisputes_ThriftGroupId_Status",
                table: "ThriftDisputes",
                columns: new[] { "ThriftGroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ThriftDisputes_Status",
                table: "ThriftDisputes",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminInvitations");

            migrationBuilder.DropTable(
                name: "ThriftDisputes");

            migrationBuilder.DropIndex(
                name: "IX_AdminProfiles_IsDeleted_IsActive_Role",
                table: "AdminProfiles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "AdminProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "AdminProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "AdminProfiles");
        }
    }
}
