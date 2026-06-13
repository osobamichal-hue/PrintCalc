using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCalculationAdvancedFieldsAndModelGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calculations_Printers_PrinterId",
                table: "Calculations");

            migrationBuilder.AddColumn<decimal>(
                name: "BboxXmm",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BboxYmm",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BboxZmm",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstimateSource",
                table: "PrintModels",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "GeometryWarnings",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurfaceCm2",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VolumeCm3",
                table: "PrintModels",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountedSubtotal",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PostProcessingCost",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PostProcessingHourlyRate",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PostProcessingHours",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityDiscountAmount",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuantityDiscountPercent",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SlicingFeeCost",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SlicingFeePerModel",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WasteCoefficientPercent",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddForeignKey(
                name: "FK_Calculations_Printers_PrinterId",
                table: "Calculations",
                column: "PrinterId",
                principalTable: "Printers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calculations_Printers_PrinterId",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "BboxXmm",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "BboxYmm",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "BboxZmm",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "EstimateSource",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "GeometryWarnings",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "SurfaceCm2",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "VolumeCm3",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "DiscountedSubtotal",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "PostProcessingCost",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "PostProcessingHourlyRate",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "PostProcessingHours",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "QuantityDiscountAmount",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "QuantityDiscountPercent",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "SlicingFeeCost",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "SlicingFeePerModel",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "WasteCoefficientPercent",
                table: "Calculations");

            migrationBuilder.AddForeignKey(
                name: "FK_Calculations_Printers_PrinterId",
                table: "Calculations",
                column: "PrinterId",
                principalTable: "Printers",
                principalColumn: "Id");
        }
    }
}
