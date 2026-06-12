using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAMA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLatestResultDenormalizationToChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset?>(
                name: "LatestCheckedAt",
                table: "Checks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestErrorMessage",
                table: "Checks",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestResponseTimeMs",
                table: "Checks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatestStatus",
                table: "Checks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            // Backfill from existing CheckResults using DISTINCT ON for efficiency
            migrationBuilder.Sql("""
                UPDATE "Checks" c
                SET
                    "LatestStatus"        = latest."Status",
                    "LatestCheckedAt"     = latest."CheckedAt",
                    "LatestResponseTimeMs"= latest."ResponseTimeMs",
                    "LatestErrorMessage"  = latest."ErrorMessage"
                FROM (
                    SELECT DISTINCT ON ("CheckId")
                        "CheckId", "Status", "CheckedAt", "ResponseTimeMs", "ErrorMessage"
                    FROM "CheckResults"
                    ORDER BY "CheckId", "CheckedAt" DESC
                ) latest
                WHERE c."Id" = latest."CheckId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LatestCheckedAt",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "LatestErrorMessage",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "LatestResponseTimeMs",
                table: "Checks");

            migrationBuilder.DropColumn(
                name: "LatestStatus",
                table: "Checks");
        }
    }
}
