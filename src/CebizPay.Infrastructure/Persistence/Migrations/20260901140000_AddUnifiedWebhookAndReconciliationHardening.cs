#nullable disable
#pragma warning disable CA1861
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddUnifiedWebhookAndReconciliationHardening : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add ReconciliationRecords Table
        migrationBuilder.CreateTable(
            name: "ReconciliationRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReconciliationType = table.Column<int>(type: "integer", nullable: false),
                SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ExpectedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                ReconciledAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                DiscrepancyReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                SafeMetadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                AttemptCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                NextPollAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastPolledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReconciliationRecords", x => x.Id);
            });

        // 2. Add RecoveryOutstandingRecords Table
        migrationBuilder.CreateTable(
            name: "RecoveryOutstandingRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                WalletId = table.Column<Guid>(type: "uuid", nullable: false),
                SourceTransactionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SourceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                Provider = table.Column<int>(type: "integer", nullable: false),
                AmountOwed = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                AmountRecovered = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                LastActionDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RecoveryOutstandingRecords", x => x.Id);
            });

        // 3. Extend WebhookEvents Table
        migrationBuilder.AddColumn<string>(
            name: "CorrelationReference",
            table: "WebhookEvents",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AttemptCount",
            table: "WebhookEvents",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "MaxAttempts",
            table: "WebhookEvents",
            type: "integer",
            nullable: false,
            defaultValue: 5);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextRetryAtUtc",
            table: "WebhookEvents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockedUntilUtc",
            table: "WebhookEvents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LockedBy",
            table: "WebhookEvents",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        // 4. Extend ComplianceWebhookEvents Table
        migrationBuilder.AddColumn<string>(
            name: "CorrelationReference",
            table: "ComplianceWebhookEvents",
            type: "character varying(150)",
            maxLength: 150,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "AttemptCount",
            table: "ComplianceWebhookEvents",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "MaxAttempts",
            table: "ComplianceWebhookEvents",
            type: "integer",
            nullable: false,
            defaultValue: 5);

        migrationBuilder.AddColumn<DateTime>(
            name: "NextRetryAtUtc",
            table: "ComplianceWebhookEvents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockedUntilUtc",
            table: "ComplianceWebhookEvents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LockedBy",
            table: "ComplianceWebhookEvents",
            type: "character varying(128)",
            maxLength: 128,
            nullable: true);

        // Indexes
        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationRecords_SourceReference",
            table: "ReconciliationRecords",
            column: "SourceReference");

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationRecords_Status_NextPollAtUtc_CreatedAtUtc",
            table: "ReconciliationRecords",
            columns: new[] { "Status", "NextPollAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationRecords_Provider_ProviderReference",
            table: "ReconciliationRecords",
            columns: new[] { "Provider", "ProviderReference" });

        migrationBuilder.CreateIndex(
            name: "IX_RecoveryOutstandingRecords_SourceReference",
            table: "RecoveryOutstandingRecords",
            column: "SourceReference");

        migrationBuilder.CreateIndex(
            name: "IX_RecoveryOutstandingRecords_WalletId_Status",
            table: "RecoveryOutstandingRecords",
            columns: new[] { "WalletId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEvents_CorrelationReference",
            table: "WebhookEvents",
            column: "CorrelationReference");

        migrationBuilder.CreateIndex(
            name: "IX_WebhookEvents_Status_NextRetryAtUtc_CreatedAtUtc",
            table: "WebhookEvents",
            columns: new[] { "Status", "NextRetryAtUtc", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceWebhookEvents_CorrelationReference",
            table: "ComplianceWebhookEvents",
            column: "CorrelationReference");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceWebhookEvents_Status_NextRetryAtUtc_CreatedAtUtc",
            table: "ComplianceWebhookEvents",
            columns: new[] { "Status", "NextRetryAtUtc", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReconciliationRecords");
        migrationBuilder.DropTable(name: "RecoveryOutstandingRecords");

        migrationBuilder.DropColumn(name: "CorrelationReference", table: "WebhookEvents");
        migrationBuilder.DropColumn(name: "AttemptCount", table: "WebhookEvents");
        migrationBuilder.DropColumn(name: "MaxAttempts", table: "WebhookEvents");
        migrationBuilder.DropColumn(name: "NextRetryAtUtc", table: "WebhookEvents");
        migrationBuilder.DropColumn(name: "LockedUntilUtc", table: "WebhookEvents");
        migrationBuilder.DropColumn(name: "LockedBy", table: "WebhookEvents");

        migrationBuilder.DropColumn(name: "CorrelationReference", table: "ComplianceWebhookEvents");
        migrationBuilder.DropColumn(name: "AttemptCount", table: "ComplianceWebhookEvents");
        migrationBuilder.DropColumn(name: "MaxAttempts", table: "ComplianceWebhookEvents");
        migrationBuilder.DropColumn(name: "NextRetryAtUtc", table: "ComplianceWebhookEvents");
        migrationBuilder.DropColumn(name: "LockedUntilUtc", table: "ComplianceWebhookEvents");
        migrationBuilder.DropColumn(name: "LockedBy", table: "ComplianceWebhookEvents");
    }
}
