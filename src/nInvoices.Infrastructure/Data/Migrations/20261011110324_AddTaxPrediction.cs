using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxPrediction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessExpenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Supplier = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    IndirectTax = table.Column<string>(type: "TEXT", nullable: false),
                    ReverseCharge = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    DeductiblePercent = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessExpenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecurringExpenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    FromMonth = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ToMonth = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringExpenses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxPayments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Year = table.Column<int>(type: "INTEGER", nullable: false),
                    Period = table.Column<int>(type: "INTEGER", nullable: false),
                    Form = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxPayments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    RegimeId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Values = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessExpenses_OwnerId",
                table: "BusinessExpenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessExpenses_OwnerId_Date",
                table: "BusinessExpenses",
                columns: new[] { "OwnerId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_OwnerId",
                table: "RecurringExpenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_OwnerId",
                table: "TaxPayments",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxPayments_OwnerId_Year_Period_Form",
                table: "TaxPayments",
                columns: new[] { "OwnerId", "Year", "Period", "Form" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxProfiles_OwnerId",
                table: "TaxProfiles",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessExpenses");

            migrationBuilder.DropTable(
                name: "RecurringExpenses");

            migrationBuilder.DropTable(
                name: "TaxPayments");

            migrationBuilder.DropTable(
                name: "TaxProfiles");
        }
    }
}
