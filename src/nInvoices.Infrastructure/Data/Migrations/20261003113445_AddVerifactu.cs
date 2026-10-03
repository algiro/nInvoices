using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifactu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComplianceValues",
                table: "Taxes",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "VerifactuRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InvoiceId = table.Column<long>(type: "INTEGER", nullable: false),
                    IssuerTaxId = table.Column<string>(type: "TEXT", maxLength: 9, nullable: false),
                    InvoiceNumber = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    IssueDate = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    InvoiceType = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    TotalTax = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalAmount = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    PreviousHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Xml = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifactuRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerifactuRecords_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuRecords_InvoiceId",
                table: "VerifactuRecords",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuRecords_OwnerId",
                table: "VerifactuRecords",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuRecords_OwnerId_Sequence",
                table: "VerifactuRecords",
                columns: new[] { "OwnerId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerifactuRecords");

            migrationBuilder.DropColumn(
                name: "ComplianceValues",
                table: "Taxes");
        }
    }
}
