using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCountry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserCountries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCountries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCountries_OwnerId",
                table: "UserCountries",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserCountries");
        }
    }
}
