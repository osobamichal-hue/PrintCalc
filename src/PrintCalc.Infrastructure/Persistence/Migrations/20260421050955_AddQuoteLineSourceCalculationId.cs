using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuoteLineSourceCalculationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceCalculationId",
                table: "QuoteLines",
                type: "INTEGER",
                nullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLines_Calculations_SourceCalculationId",
                table: "QuoteLines");

            migrationBuilder.DropIndex(
                name: "IX_QuoteLines_SourceCalculationId",
                table: "QuoteLines");

            migrationBuilder.DropColumn(
                name: "SourceCalculationId",
                table: "QuoteLines");
        }
    }
}
