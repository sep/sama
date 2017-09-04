using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace sama.Migrations
{
    public partial class GeneralizeEndpoint : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JsonConfig",
                table: "Endpoints",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.Sql(@"UPDATE Endpoints SET JsonConfig=json_set(JsonConfig, '$.Location', Location);");
            migrationBuilder.Sql(@"UPDATE Endpoints SET JsonConfig=json_set(JsonConfig, '$.ResponseMatch', ResponseMatch);");
            migrationBuilder.Sql(@"UPDATE Endpoints SET JsonConfig=json_set(JsonConfig, '$.StatusCodes', json('[' || StatusCodes || ']'));");

            migrationBuilder.RenameTable("Endpoints", newName: "OldEndpoints");

            migrationBuilder.CreateTable(
                name: "Endpoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    JsonConfig = table.Column<string>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Endpoints", x => x.Id);
                });

            migrationBuilder.Sql(@"INSERT INTO Endpoints (Id, Enabled, JsonConfig, Kind, Name) SELECT Id, Enabled, JsonConfig, 0, Name FROM OldEndpoints;");

            migrationBuilder.DropTable("OldEndpoints");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotImplementedException();
        }
    }
}
