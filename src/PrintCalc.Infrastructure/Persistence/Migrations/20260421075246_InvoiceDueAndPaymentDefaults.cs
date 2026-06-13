using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceDueAndPaymentDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Invoices",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceDueDays",
                table: "Customers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredPaymentMethod",
                table: "Customers",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "InvoiceDueDays",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PreferredPaymentMethod",
                table: "Customers");
        }
    }
}
