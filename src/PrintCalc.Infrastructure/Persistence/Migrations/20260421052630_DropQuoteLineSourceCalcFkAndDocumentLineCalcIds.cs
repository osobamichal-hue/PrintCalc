using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropQuoteLineSourceCalcFkAndDocumentLineCalcIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLines_Calculations_SourceCalculationId",
                table: "QuoteLines");

            migrationBuilder.DropIndex(
                name: "IX_QuoteLines_SourceCalculationId",
                table: "QuoteLines");

            migrationBuilder.AddColumn<int>(
                name: "SourceCalculationId",
                table: "OrderLines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceCalculationId",
                table: "InvoiceLines",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceCalculationId",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "SourceCalculationId",
                table: "InvoiceLines");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_SourceCalculationId",
                table: "QuoteLines",
                column: "SourceCalculationId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLines_Calculations_SourceCalculationId",
                table: "QuoteLines",
                column: "SourceCalculationId",
                principalTable: "Calculations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
