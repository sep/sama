using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

namespace sama.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Endpoints",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(nullable: false),
                    Location = table.Column<string>(nullable: false),
                    Name = table.Column<string>(maxLength: 64, nullable: false),
                    ResponseMatch = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endpoints", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Endpoints");
        }
    }
}
