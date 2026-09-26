using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace nInvoices.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_CountryCode",
                table: "HolidayCalendars");

            // The seeded sequence row (Id = 1) is no longer part of the model. If it was ever used
            // it holds the existing numbering, so keep it for the legacy owner; an untouched one
            // carries nothing and would only show up as unowned data, so drop it.
            migrationBuilder.Sql(
                "DELETE FROM \"InvoiceSequence\" WHERE \"Id\" = 1 AND \"CurrentValue\" = 1 AND \"UpdatedAt\" IS NULL;");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "WorkDays",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "WorkDayProjects",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Taxes",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Rates",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Projects",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "MonthlyReportTemplates",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "InvoiceTemplates",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "InvoiceTaxLines",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "InvoiceSequence",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Invoices",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "InvoiceEmails",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "ImageAssets",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "HolidayRules",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "HolidayCalendars",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Expenses",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "EmailTemplates",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerId",
                table: "Customers",
                type: "TEXT",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDays_OwnerId",
                table: "WorkDays",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkDayProjects_OwnerId",
                table: "WorkDayProjects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Taxes_OwnerId",
                table: "Taxes",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Rates_OwnerId",
                table: "Rates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyReportTemplates_OwnerId",
                table: "MonthlyReportTemplates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTemplates_OwnerId",
                table: "InvoiceTemplates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceTaxLines_OwnerId",
                table: "InvoiceTaxLines",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OwnerId",
                table: "Invoices",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEmails_OwnerId",
                table: "InvoiceEmails",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ImageAssets_OwnerId",
                table: "ImageAssets",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayRules_OwnerId",
                table: "HolidayRules",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_OwnerId",
                table: "HolidayCalendars",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_OwnerId_CountryCode",
                table: "HolidayCalendars",
                columns: new[] { "OwnerId", "CountryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_OwnerId",
                table: "Expenses",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OwnerId",
                table: "EmailTemplates",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_OwnerId",
                table: "Customers",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkDays_OwnerId",
                table: "WorkDays");

            migrationBuilder.DropIndex(
                name: "IX_WorkDayProjects_OwnerId",
                table: "WorkDayProjects");

            migrationBuilder.DropIndex(
                name: "IX_Taxes_OwnerId",
                table: "Taxes");

            migrationBuilder.DropIndex(
                name: "IX_Rates_OwnerId",
                table: "Rates");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OwnerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_MonthlyReportTemplates_OwnerId",
                table: "MonthlyReportTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTemplates_OwnerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceTaxLines_OwnerId",
                table: "InvoiceTaxLines");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceSequence_OwnerId",
                table: "InvoiceSequence");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OwnerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceEmails_OwnerId",
                table: "InvoiceEmails");

            migrationBuilder.DropIndex(
                name: "IX_ImageAssets_OwnerId",
                table: "ImageAssets");

            migrationBuilder.DropIndex(
                name: "IX_HolidayRules_OwnerId",
                table: "HolidayRules");

            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_OwnerId",
                table: "HolidayCalendars");

            migrationBuilder.DropIndex(
                name: "IX_HolidayCalendars_OwnerId_CountryCode",
                table: "HolidayCalendars");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_OwnerId",
                table: "Expenses");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_OwnerId",
                table: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Customers_OwnerId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "WorkDays");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "WorkDayProjects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Taxes");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Rates");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "MonthlyReportTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InvoiceTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InvoiceTaxLines");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InvoiceSequence");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "InvoiceEmails");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ImageAssets");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "HolidayRules");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "HolidayCalendars");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "IX_HolidayCalendars_CountryCode",
                table: "HolidayCalendars",
                column: "CountryCode",
                unique: true);
        }
    }
}
