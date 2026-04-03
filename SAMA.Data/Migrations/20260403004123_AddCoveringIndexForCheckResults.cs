using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAMA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCoveringIndexForCheckResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckResults_CheckId_CheckedAt",
                table: "CheckResults");

            migrationBuilder.CreateIndex(
                name: "IX_CheckResults_CheckId_CheckedAt_Covering",
                table: "CheckResults",
                columns: new[] { "CheckId", "CheckedAt" },
                descending: new[] { false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "Status", "ResponseTimeMs", "ErrorMessage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CheckResults_CheckId_CheckedAt_Covering",
                table: "CheckResults");

            migrationBuilder.CreateIndex(
                name: "IX_CheckResults_CheckId_CheckedAt",
                table: "CheckResults",
                columns: new[] { "CheckId", "CheckedAt" },
                descending: new[] { false, true });
        }
    }
}
