using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SAMA.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DashboardMessage",
                table: "Workspaces",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DashboardMessage",
                table: "Workspaces");
        }
    }
}
