using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFilamentStockBatchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "FilamentStocks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "FilamentStocks",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FilamentStocks",
                type: "TEXT",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "FilamentStocks");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                table: "FilamentStocks");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FilamentStocks");
        }
    }
}
