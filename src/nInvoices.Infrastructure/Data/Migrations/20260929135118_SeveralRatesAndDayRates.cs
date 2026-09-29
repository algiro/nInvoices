using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeveralRatesAndDayRates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rates_CustomerId_Type",
                table: "Rates");

            migrationBuilder.AddColumn<long>(
                name: "RateId",
                table: "WorkDays",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Rates",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rates_CustomerId",
                table: "Rates",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rates_CustomerId",
                table: "Rates");

            migrationBuilder.DropColumn(
                name: "RateId",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "Rates");

            // One rate per type per customer again: the oldest of each type is kept
            migrationBuilder.Sql(
                "DELETE FROM \"Rates\" WHERE \"Id\" NOT IN " +
                "(SELECT MIN(\"Id\") FROM \"Rates\" GROUP BY \"CustomerId\", \"Type\");");

            migrationBuilder.CreateIndex(
                name: "IX_Rates_CustomerId_Type",
                table: "Rates",
                columns: new[] { "CustomerId", "Type" },
                unique: true);
        }
    }
}
