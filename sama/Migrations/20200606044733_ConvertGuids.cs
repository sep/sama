using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace sama.Migrations
{
    public partial class ConvertGuids : Migration
    {
        private const string ID_CONVERSION = @"
SET Id = lower( hex(substr(Id, 4, 1)) ||
                hex(substr(Id, 3, 1)) ||
                hex(substr(Id, 2, 1)) ||
                hex(substr(Id, 1, 1)) || '-' ||
                hex(substr(Id, 6, 1)) ||
                hex(substr(Id, 5, 1)) || '-' ||
                hex(substr(Id, 8, 1)) ||
                hex(substr(Id, 7, 1)) || '-' ||
                hex(substr(Id, 9, 2)) || '-' ||
                hex(substr(Id, 11, 6))
              )
WHERE typeof(Id) == 'blob';
";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE Settings {ID_CONVERSION}");
            migrationBuilder.Sql($"UPDATE Users {ID_CONVERSION}");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotImplementedException();
        }
    }
}
