using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerCustomerInvoiceNumbering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence");

            migrationBuilder.AddColumn<long>(
                name: "CustomerId",
                table: "InvoiceSequence",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            // The user-wide counter becomes one counter per customer. Each existing customer starts
            // at the user's current value, so no number already handed out can be issued again.
            migrationBuilder.Sql(
                "INSERT INTO \"InvoiceSequence\" (\"OwnerId\", \"CustomerId\", \"CurrentValue\", \"CreatedAt\") " +
                "SELECT c.\"OwnerId\", c.\"Id\", s.\"CurrentValue\", s.\"CreatedAt\" " +
                "FROM \"Customers\" c JOIN \"InvoiceSequence\" s ON s.\"OwnerId\" = c.\"OwnerId\" " +
                "WHERE s.\"CustomerId\" = 0;");
            migrationBuilder.Sql("DELETE FROM \"InvoiceSequence\" WHERE \"CustomerId\" = 0;");

            migrationBuilder.AddColumn<string>(
                name: "NumberFormat",
                table: "Customers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            // Back to one counter per user: the highest one wins, so no number is reissued
            migrationBuilder.Sql(
                "UPDATE \"InvoiceSequence\" SET \"CurrentValue\" = (SELECT MAX(s2.\"CurrentValue\") " +
                "FROM \"InvoiceSequence\" s2 WHERE s2.\"OwnerId\" = \"InvoiceSequence\".\"OwnerId\");");
            migrationBuilder.Sql(
                "DELETE FROM \"InvoiceSequence\" WHERE \"Id\" NOT IN " +
                "(SELECT MIN(\"Id\") FROM \"InvoiceSequence\" GROUP BY \"OwnerId\");");

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
    }
}
