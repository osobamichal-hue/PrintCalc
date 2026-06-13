using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModelCatalogAndDesignTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ModelDesignCost",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ModelDesignHourlyRate",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ModelDesignHours",
                table: "Calculations",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "PrintModelId",
                table: "Calculations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PrintModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    FileType = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    EstimatedMaterialGrams = table.Column<decimal>(type: "TEXT", nullable: true),
                    EstimatedPrintHours = table.Column<decimal>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintModels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calculations_PrintModelId",
                table: "Calculations",
                column: "PrintModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Calculations_PrintModels_PrintModelId",
                table: "Calculations",
                column: "PrintModelId",
                principalTable: "PrintModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Calculations_PrintModels_PrintModelId",
                table: "Calculations");

            migrationBuilder.DropTable(
                name: "PrintModels");

            migrationBuilder.DropIndex(
                name: "IX_Calculations_PrintModelId",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "ModelDesignCost",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "ModelDesignHourlyRate",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "ModelDesignHours",
                table: "Calculations");

            migrationBuilder.DropColumn(
                name: "PrintModelId",
                table: "Calculations");
        }
    }
}
