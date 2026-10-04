using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEInvoiceSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EInvoiceSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceEInvoiceId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Environment = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    StatusName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CancellationStatus = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 1600, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EInvoiceSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EInvoiceSubmissions_InvoiceEInvoices_InvoiceEInvoiceId",
                        column: x => x.InvoiceEInvoiceId,
                        principalTable: "InvoiceEInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EInvoiceSubmissions_InvoiceEInvoiceId_ChannelId",
                table: "EInvoiceSubmissions",
                columns: new[] { "InvoiceEInvoiceId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EInvoiceSubmissions_OwnerId",
                table: "EInvoiceSubmissions",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EInvoiceSubmissions");
        }
    }
}
