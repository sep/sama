using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAMA.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceIntervalSecondsWithSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Schedule",
                table: "Checks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "60");

            migrationBuilder.Sql(
                """UPDATE "Checks" SET "Schedule" = "IntervalSeconds"::text""");

            migrationBuilder.DropColumn(
                name: "IntervalSeconds",
                table: "Checks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IntervalSeconds",
                table: "Checks",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.Sql(
                """UPDATE "Checks" SET "IntervalSeconds" = CASE WHEN "Schedule" ~ '^\d+$' THEN "Schedule"::integer ELSE 60 END""");

            migrationBuilder.DropColumn(
                name: "Schedule",
                table: "Checks");
        }
    }
}
