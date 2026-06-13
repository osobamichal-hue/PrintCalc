using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockAlertsAndCalculationIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CalculationId",
                table: "StockMovements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinStockKg",
                table: "FilamentTypes",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CalculationId",
                table: "StockMovements",
                column: "CalculationId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Calculations_CalculationId",
                table: "StockMovements",
                column: "CalculationId",
                principalTable: "Calculations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Calculations_CalculationId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_CalculationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "CalculationId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "MinStockKg",
                table: "FilamentTypes");
        }
    }
}
