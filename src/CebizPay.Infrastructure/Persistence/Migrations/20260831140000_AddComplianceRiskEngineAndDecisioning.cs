using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace CebizPay.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddComplianceRiskEngineAndDecisioning : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RiskAssessments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                RiskRating = table.Column<int>(type: "integer", nullable: false),
                CddLevel = table.Column<int>(type: "integer", nullable: false),
                EddRequired = table.Column<bool>(type: "boolean", nullable: false),
                RulesetVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                EvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsCurrent = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskAssessments", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "RiskFactorResults",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                RuleId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                RuleName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RiskRating = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                EvidenceReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Severity = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RiskFactorResults", x => x.Id);
                table.ForeignKey(
                    name: "FK_RiskFactorResults_RiskAssessments_RiskAssessmentId",
                    column: x => x.RiskAssessmentId,
                    principalTable: "RiskAssessments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CddProfiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                RiskRating = table.Column<int>(type: "integer", nullable: false),
                CddLevel = table.Column<int>(type: "integer", nullable: false),
                Tier = table.Column<int>(type: "integer", nullable: true),
                LatestRiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastEvaluatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CddProfiles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "EddCases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CaseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                RiskAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                TriggerReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                RequiredInformation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                SubmittedInformation = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                AssignedReviewerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ReviewedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                SeniorManagementApprovalRequired = table.Column<bool>(type: "boolean", nullable: false),
                SeniorManagementApproverId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                Decision = table.Column<int>(type: "integer", nullable: true),
                DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EddCases", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                Decision = table.Column<int>(type: "integer", nullable: false),
                RiskRating = table.Column<int>(type: "integer", nullable: false),
                CddLevel = table.Column<int>(type: "integer", nullable: false),
                EddStatus = table.Column<int>(type: "integer", nullable: true),
                DecisionReasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                RulesetVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                DecidedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                IsManualOverride = table.Column<bool>(type: "boolean", nullable: false),
                OverrideReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceDecisions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ComplianceRestrictions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectType = table.Column<int>(type: "integer", nullable: false),
                SubjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                RestrictionType = table.Column<int>(type: "integer", nullable: false),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                DailyCapAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                SingleCapAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                PlacedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                PlacedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ReleasedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                ReleaseReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ComplianceRestrictions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RiskAssessments_EvaluatedAtUtc",
            table: "RiskAssessments",
            column: "EvaluatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_RiskAssessments_OrganizationId",
            table: "RiskAssessments",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_RiskAssessments_SubjectType_SubjectId_IsCurrent",
            table: "RiskAssessments",
            columns: new[] { "SubjectType", "SubjectId", "IsCurrent" });

        migrationBuilder.CreateIndex(
            name: "IX_RiskFactorResults_RiskAssessmentId",
            table: "RiskFactorResults",
            column: "RiskAssessmentId");

        migrationBuilder.CreateIndex(
            name: "IX_RiskFactorResults_RuleId",
            table: "RiskFactorResults",
            column: "RuleId");

        migrationBuilder.CreateIndex(
            name: "IX_CddProfiles_OrganizationId",
            table: "CddProfiles",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_CddProfiles_Status",
            table: "CddProfiles",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_CddProfiles_SubjectType_SubjectId",
            table: "CddProfiles",
            columns: new[] { "SubjectType", "SubjectId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EddCases_CaseNumber",
            table: "EddCases",
            column: "CaseNumber",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_EddCases_OrganizationId",
            table: "EddCases",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_EddCases_Status",
            table: "EddCases",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_EddCases_SubjectType_SubjectId",
            table: "EddCases",
            columns: new[] { "SubjectType", "SubjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDecisions_EffectiveFromUtc",
            table: "ComplianceDecisions",
            column: "EffectiveFromUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDecisions_OrganizationId",
            table: "ComplianceDecisions",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceDecisions_SubjectType_SubjectId_IsActive",
            table: "ComplianceDecisions",
            columns: new[] { "SubjectType", "SubjectId", "IsActive" });

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceRestrictions_OrganizationId",
            table: "ComplianceRestrictions",
            column: "OrganizationId");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceRestrictions_PlacedAtUtc",
            table: "ComplianceRestrictions",
            column: "PlacedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_ComplianceRestrictions_SubjectType_SubjectId_IsActive",
            table: "ComplianceRestrictions",
            columns: new[] { "SubjectType", "SubjectId", "IsActive" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ComplianceRestrictions");
        migrationBuilder.DropTable(name: "ComplianceDecisions");
        migrationBuilder.DropTable(name: "EddCases");
        migrationBuilder.DropTable(name: "CddProfiles");
        migrationBuilder.DropTable(name: "RiskFactorResults");
        migrationBuilder.DropTable(name: "RiskAssessments");
    }
}
