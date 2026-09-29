using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRateAndHoursToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Hours",
                table: "Invoices",
                type: "TEXT",
                precision: 9,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RateId",
                table: "Invoices",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Hours",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RateId",
                table: "Invoices");
        }
    }
}
