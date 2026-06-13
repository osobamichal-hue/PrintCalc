using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineOrderTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceOrderId",
                table: "InvoiceLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceOrderLineId",
                table: "InvoiceLines",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceOrderId",
                table: "InvoiceLines");

            migrationBuilder.DropColumn(
                name: "SourceOrderLineId",
                table: "InvoiceLines");
        }
    }
}
