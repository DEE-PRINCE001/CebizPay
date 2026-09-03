#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase6ECustomerSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EscalatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FirstResponseAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SlaDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsSlaBreached = table.Column<bool>(type: "boolean", nullable: false),
                    SlaBreachedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TicketNumber",
                table: "SupportTickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_OrganizationId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "OrganizationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_SlaDueAtUtc",
                table: "SupportTickets",
                columns: new[] { "Status", "SlaDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Priority_Status",
                table: "SupportTickets",
                columns: new[] { "Priority", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Category_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "Category", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_IsSlaBreached_SlaDueAtUtc",
                table: "SupportTickets",
                columns: new[] { "IsSlaBreached", "SlaDueAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId_IdempotencyKey",
                table: "SupportTickets",
                columns: new[] { "UserId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketMessages_TicketId_CreatedAtUtc",
                table: "TicketMessages",
                columns: new[] { "TicketId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");
        }
    }
}
