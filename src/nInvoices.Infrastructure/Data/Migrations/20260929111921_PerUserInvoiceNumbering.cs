using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerUserInvoiceNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NumberFormat",
                table: "InvoiceSequence",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceSequence_Customers_CustomerId",
                table: "InvoiceSequence");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_CustomerId",
                table: "InvoiceSequence");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_OwnerId_CustomerId",
                table: "InvoiceSequence");

            // Numbering goes back to one counter per user. The highest counter wins, so no
            // number already handed out can be issued again.
            migrationBuilder.Sql(
                "UPDATE \"InvoiceSequence\" SET \"CurrentValue\" = (SELECT MAX(s2.\"CurrentValue\") " +
                "FROM \"InvoiceSequence\" s2 WHERE s2.\"OwnerId\" = \"InvoiceSequence\".\"OwnerId\");");
            migrationBuilder.Sql(
                "DELETE FROM \"InvoiceSequence\" WHERE \"Id\" NOT IN " +
                "(SELECT MIN(\"Id\") FROM \"InvoiceSequence\" GROUP BY \"OwnerId\");");

            // A pattern set on the customers becomes the user's, when they all use the same one
            migrationBuilder.Sql(
                "UPDATE \"InvoiceSequence\" SET \"NumberFormat\" = (SELECT MIN(c.\"NumberFormat\") FROM \"Customers\" c " +
                "WHERE c.\"OwnerId\" = \"InvoiceSequence\".\"OwnerId\" AND c.\"NumberFormat\" IS NOT NULL) " +
                "WHERE (SELECT COUNT(DISTINCT c.\"NumberFormat\") FROM \"Customers\" c " +
                "WHERE c.\"OwnerId\" = \"InvoiceSequence\".\"OwnerId\" AND c.\"NumberFormat\" IS NOT NULL) = 1;");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "InvoiceSequence");

            migrationBuilder.DropColumn(
                name: "NumberFormat",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence",
                column: "OwnerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence");

            migrationBuilder.DropColumn(
                name: "NumberFormat",
                table: "InvoiceSequence");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "InvoiceSequence",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "NumberFormat",
                table: "Customers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            // Each customer starts at the user's counter (the pattern is not carried back)
            migrationBuilder.Sql(
                "INSERT INTO \"InvoiceSequence\" (\"OwnerId\", \"CustomerId\", \"CurrentValue\", \"CreatedAt\") " +
                "SELECT c.\"OwnerId\", c.\"Id\", s.\"CurrentValue\", s.\"CreatedAt\" " +
                "FROM \"Customers\" c JOIN \"InvoiceSequence\" s ON s.\"OwnerId\" = c.\"OwnerId\" " +
                "WHERE s.\"CustomerId\" = 0;");
            migrationBuilder.Sql("DELETE FROM \"InvoiceSequence\" WHERE \"CustomerId\" = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequence_CustomerId",
                table: "InvoiceSequence",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequence_OwnerId_CustomerId",
                table: "InvoiceSequence",
                columns: new[] { "OwnerId", "CustomerId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceSequence_Customers_CustomerId",
                table: "InvoiceSequence",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
