using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHolidayCalendars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HolidayCountry",
                table: "Customers",
                type: "TEXT",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HolidayCalendars",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayCalendars", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HolidayRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    HolidayCalendarId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Month = table.Column<int>(type: "INTEGER", nullable: true),
                    Day = table.Column<int>(type: "INTEGER", nullable: true),
                    EasterOffset = table.Column<int>(type: "INTEGER", nullable: true),
                    Weekday = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Occurrence = table.Column<int>(type: "INTEGER", nullable: true),
                    FromYear = table.Column<int>(type: "INTEGER", nullable: true),
                    ToYear = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HolidayRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HolidayRules_HolidayCalendars_HolidayCalendarId",
                        column: x => x.HolidayCalendarId,
                        principalTable: "HolidayCalendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_CountryCode",
                table: "HolidayCalendars",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HolidayRules_HolidayCalendarId",
                table: "HolidayRules",
                column: "HolidayCalendarId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HolidayRules");

            migrationBuilder.DropTable(
                name: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "HolidayCountry",
                table: "Customers");
        }
    }
}
