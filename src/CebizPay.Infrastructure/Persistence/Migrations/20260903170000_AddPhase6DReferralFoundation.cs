#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase6DReferralFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardAmountPerSuccessfulReferral = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MaximumSuccessfulReferralsPerUser = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReferredUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReferralCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    QualificationStatus = table.Column<int>(type: "integer", nullable: false),
                    RewardEligibility = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    QualifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QualifyingDepositReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    QualifyingDepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RiskReviewNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReferralRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralRelationshipId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ReferredUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EligibleAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LedgerTransactionReference = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralRewards", x => x.Id);
                });

            // Indexes for ReferralSettings
            migrationBuilder.CreateIndex(
                name: "IX_ReferralSettings_IsActive",
                table: "ReferralSettings",
                column: "IsActive");

            // Indexes for ReferralCodes
            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_Code",
                table: "ReferralCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralCodes_UserId_IsActive",
                table: "ReferralCodes",
                columns: new[] { "UserId", "IsActive" });

            // Indexes for ReferralRelationships
            migrationBuilder.CreateIndex(
                name: "IX_ReferralRelationships_ReferredUserId",
                table: "ReferralRelationships",
                column: "ReferredUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRelationships_ReferrerUserId",
                table: "ReferralRelationships",
                column: "ReferrerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRelationships_ReferrerUserId_QualificationStatus",
                table: "ReferralRelationships",
                columns: new[] { "ReferrerUserId", "QualificationStatus" });

            // Indexes for ReferralRewards
            migrationBuilder.CreateIndex(
                name: "IX_ReferralRewards_ReferralRelationshipId",
                table: "ReferralRewards",
                column: "ReferralRelationshipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRewards_ReferrerUserId",
                table: "ReferralRewards",
                column: "ReferrerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRewards_ReferrerUserId_Status",
                table: "ReferralRewards",
                columns: new[] { "ReferrerUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ReferralRewards");
            migrationBuilder.DropTable(name: "ReferralRelationships");
            migrationBuilder.DropTable(name: "ReferralCodes");
            migrationBuilder.DropTable(name: "ReferralSettings");
        }
    }
}
