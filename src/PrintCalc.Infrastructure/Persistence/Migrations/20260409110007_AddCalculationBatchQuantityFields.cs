using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalculationBatchQuantityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PiecesPerBuild",
                table: "Calculations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PrintRuns",
                table: "Calculations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RequiredPieces",
                table: "Calculations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PiecesPerBuild",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "PrintRuns",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "RequiredPieces",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "Calculations");
        }
    }
}
