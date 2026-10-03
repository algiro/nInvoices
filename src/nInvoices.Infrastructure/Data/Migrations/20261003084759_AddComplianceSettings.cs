using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LegalName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TaxId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address_Street = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Address_HouseNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Address_City = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Address_ZipCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Address_State = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Address_Country = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Values = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSettings_OwnerId",
                table: "ComplianceSettings",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSettings_OwnerId_CountryCode",
                table: "ComplianceSettings",
                columns: new[] { "OwnerId", "CountryCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceSettings");
        }
    }
}
