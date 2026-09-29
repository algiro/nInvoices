using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SharedTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "MonthlyReportTemplates",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "InvoiceTemplates",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "EmailTemplates",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_Shared_OwnerId_InvoiceType",
                table: "InvoiceTemplates",
                columns: new[] { "OwnerId", "InvoiceType" },
                unique: true,
                filter: "[IsActive] = 1 AND [CustomerId] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InvoiceTemplates_Shared_OwnerId_InvoiceType",
                table: "InvoiceTemplates");

            // Every template needs a customer again, so the shared ones go
            migrationBuilder.Sql("DELETE FROM \"InvoiceTemplates\" WHERE \"CustomerId\" IS NULL;");
            migrationBuilder.Sql("DELETE FROM \"MonthlyReportTemplates\" WHERE \"CustomerId\" IS NULL;");
            migrationBuilder.Sql("DELETE FROM \"EmailTemplates\" WHERE \"CustomerId\" IS NULL;");

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "MonthlyReportTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "InvoiceTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "CustomerId",
                table: "EmailTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
