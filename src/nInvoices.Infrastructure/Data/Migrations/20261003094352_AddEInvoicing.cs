using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEInvoicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComplianceValues",
                table: "Customers",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<DateTime>(
                name: "CertificateNotAfter",
                table: "ComplianceSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateSubject",
                table: "ComplianceSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateThumbprint",
                table: "ComplianceSettings",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedCertificate",
                table: "ComplianceSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedCertificatePassword",
                table: "ComplianceSettings",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvoiceEInvoices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<long>(type: "INTEGER", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    FormatId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileExtension = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceEInvoices_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEInvoices_InvoiceId_FormatId",
                table: "InvoiceEInvoices",
                columns: new[] { "InvoiceId", "FormatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEInvoices_OwnerId",
                table: "InvoiceEInvoices",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceEInvoices");

            migrationBuilder.DropColumn(
                name: "ComplianceValues",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "CertificateNotAfter",
                table: "ComplianceSettings");

            migrationBuilder.DropColumn(
                name: "CertificateSubject",
                table: "ComplianceSettings");

            migrationBuilder.DropColumn(
                name: "CertificateThumbprint",
                table: "ComplianceSettings");

            migrationBuilder.DropColumn(
                name: "ProtectedCertificate",
                table: "ComplianceSettings");

            migrationBuilder.DropColumn(
                name: "ProtectedCertificatePassword",
                table: "ComplianceSettings");
        }
    }
}
