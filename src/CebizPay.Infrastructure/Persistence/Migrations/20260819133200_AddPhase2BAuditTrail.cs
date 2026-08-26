using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2BAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "EntityType",
                table: "AuditLogs",
                newName: "ResourceType");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "AuditLogs",
                newName: "ActorId");

            migrationBuilder.RenameColumn(
                name: "DetailsJson",
                table: "AuditLogs",
                newName: "BeforeJson");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "AuditLogs",
                newName: "OccurredAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_CreatedAtUtc",
                table: "AuditLogs",
                newName: "IX_AuditLogs_OccurredAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "AfterJson",
                table: "AuditLogs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "AuditLogs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "AuditLogs",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AuditLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResourceId",
                table: "AuditLogs",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditLogs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ResourceType_ResourceId",
                table: "AuditLogs",
                columns: new[] { "ResourceType", "ResourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ActorId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_OrganizationId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_ResourceType_ResourceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AfterJson",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "AuditLogs");

            migrationBuilder.RenameColumn(
                name: "ResourceType",
                table: "AuditLogs",
                newName: "EntityType");

            migrationBuilder.RenameColumn(
                name: "OccurredAtUtc",
                table: "AuditLogs",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "BeforeJson",
                table: "AuditLogs",
                newName: "DetailsJson");

            migrationBuilder.RenameColumn(
                name: "ActorId",
                table: "AuditLogs",
                newName: "EntityId");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_OccurredAtUtc",
                table: "AuditLogs",
                newName: "IX_AuditLogs_CreatedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "AuditLogs",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");
        }
    }
}
