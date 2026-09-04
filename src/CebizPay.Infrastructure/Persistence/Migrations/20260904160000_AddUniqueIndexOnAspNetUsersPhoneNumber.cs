#nullable disable
#pragma warning disable CA1861, CA1707, CS1591, CS8618
using Microsoft.EntityFrameworkCore.Migrations;

namespace CebizPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnAspNetUsersPhoneNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reconcile and normalize existing legacy phone numbers before applying unique index
            migrationBuilder.Sql(@"
                -- 1. Remove non-digits and non-plus
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = REGEXP_REPLACE(""PhoneNumber"", '[^0-9+]', '', 'g')
                WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" != '';

                -- 2. Normalize 080... (11 digits) to +23480...
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = '+234' || SUBSTRING(""PhoneNumber"" FROM 2)
                WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" LIKE '0%' AND LENGTH(""PhoneNumber"") = 11;

                -- 3. Normalize 2340... (14 digits) to +234...
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = '+234' || SUBSTRING(""PhoneNumber"" FROM 5)
                WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" LIKE '2340%' AND LENGTH(""PhoneNumber"") = 14;

                -- 4. Normalize 234... (13 digits) to +234...
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = '+' || ""PhoneNumber""
                WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" LIKE '234%' AND LENGTH(""PhoneNumber"") = 13;

                -- 5. Normalize 10-digit without leading zero to +234...
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = '+234' || ""PhoneNumber""
                WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" NOT LIKE '+%' AND LENGTH(""PhoneNumber"") = 10;

                -- 6. For any duplicate phone numbers, retain the phone number on the oldest account
                -- and clear it (set to NULL) on subsequent duplicate accounts while logging to AuditLogs
                WITH RankedDuplicates AS (
                    SELECT ""Id"", ""PhoneNumber"",
                           ROW_NUMBER() OVER (PARTITION BY ""PhoneNumber"" ORDER BY ""CreatedAtUtc"" ASC, ""Id"" ASC) as rn
                    FROM ""AspNetUsers""
                    WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" != ''
                )
                INSERT INTO ""AuditLogs"" (""Id"", ""Action"", ""ResourceType"", ""ResourceId"", ""ActorId"", ""BeforeJson"", ""AfterJson"", ""OccurredAtUtc"")
                SELECT 
                    gen_random_uuid(),
                    'PHONE_NUMBER_DEDUPLICATION_RECONCILIATION',
                    'AspNetUsers',
                    rd.""Id""::text,
                    'SYSTEM_MIGRATION',
                    json_build_object('PhoneNumber', rd.""PhoneNumber"")::text,
                    json_build_object('PhoneNumber', NULL, 'Reason', 'Duplicate phone number cleared during migration to unique phone index; original retained by earliest user')::text,
                    NOW()
                FROM RankedDuplicates rd
                WHERE rd.rn > 1;

                WITH RankedDuplicates AS (
                    SELECT ""Id"",
                           ROW_NUMBER() OVER (PARTITION BY ""PhoneNumber"" ORDER BY ""CreatedAtUtc"" ASC, ""Id"" ASC) as rn
                    FROM ""AspNetUsers""
                    WHERE ""PhoneNumber"" IS NOT NULL AND ""PhoneNumber"" != ''
                )
                UPDATE ""AspNetUsers""
                SET ""PhoneNumber"" = NULL
                WHERE ""Id"" IN (SELECT ""Id"" FROM RankedDuplicates WHERE rn > 1);
            ");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers",
                column: "PhoneNumber",
                unique: true,
                filter: "\"PhoneNumber\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PhoneNumber",
                table: "AspNetUsers");
        }
    }
}
