#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceVerificationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerificationOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VerificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capability = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PrimaryProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ActiveProvider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    UsedFallback = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerificationEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VerificationOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Capability = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ResultStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SafeMetadata = table.Column<string>(type: "text", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RawPayloadRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationEvidences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerificationEvidences_VerificationOperations_VerificationOpe~",
                        column: x => x.VerificationOperationId,
                        principalTable: "VerificationOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceWebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    VerificationOperationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessingError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SafeMetadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceWebhookEvents_PayloadHash",
                table: "ComplianceWebhookEvents",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceWebhookEvents_Provider_ProviderEventId",
                table: "ComplianceWebhookEvents",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationEvidences_OrganizationId",
                table: "VerificationEvidences",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationEvidences_ProviderReference",
                table: "VerificationEvidences",
                column: "ProviderReference");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationEvidences_UserId",
                table: "VerificationEvidences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationEvidences_VerificationOperationId",
                table: "VerificationEvidences",
                column: "VerificationOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationOperations_IdempotencyKey",
                table: "VerificationOperations",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationOperations_OrganizationId",
                table: "VerificationOperations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationOperations_Reference",
                table: "VerificationOperations",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationOperations_UserId",
                table: "VerificationOperations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceWebhookEvents");

            migrationBuilder.DropTable(
                name: "VerificationEvidences");

            migrationBuilder.DropTable(
                name: "VerificationOperations");
        }
    }
}
