using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifactuSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VerifactuSubmissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RecordId = table.Column<long>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Csv = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1600, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifactuSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VerifactuSubmissions_VerifactuRecords_RecordId",
                        column: x => x.RecordId,
                        principalTable: "VerifactuRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuSubmissions_OwnerId",
                table: "VerifactuSubmissions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuSubmissions_RecordId",
                table: "VerifactuSubmissions",
                column: "RecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuSubmissions_Status_NextAttemptAt",
                table: "VerifactuSubmissions",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerifactuSubmissions");
        }
    }
}
