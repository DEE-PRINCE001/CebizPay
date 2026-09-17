using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobAndAnnouncementBannersAndApplicationProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationEmail",
                table: "JobPostings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationProcess",
                table: "JobPostings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "JobPostings",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BannerUrl",
                table: "Announcements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationEmail",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "ApplicationProcess",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "JobPostings");

            migrationBuilder.DropColumn(
                name: "BannerUrl",
                table: "Announcements");
        }
    }
}
