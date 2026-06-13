using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilamentTemperatureRanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TempMinC",
                table: "FilamentTypes",
                newName: "NozzleTempMinC");

            migrationBuilder.RenameColumn(
                name: "TempMaxC",
                table: "FilamentTypes",
                newName: "BedTempMinC");

            migrationBuilder.AddColumn<int>(
                name: "NozzleTempMaxC",
                table: "FilamentTypes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedTempMaxC",
                table: "FilamentTypes",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NozzleTempMaxC",
                table: "FilamentTypes");

            migrationBuilder.DropColumn(
                name: "BedTempMaxC",
                table: "FilamentTypes");

            migrationBuilder.RenameColumn(
                name: "NozzleTempMinC",
                table: "FilamentTypes",
                newName: "TempMinC");

            migrationBuilder.RenameColumn(
                name: "BedTempMinC",
                table: "FilamentTypes",
                newName: "TempMaxC");
        }
    }
}
