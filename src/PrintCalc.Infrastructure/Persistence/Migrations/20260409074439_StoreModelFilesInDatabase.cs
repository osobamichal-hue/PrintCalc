using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreModelFilesInDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "PrintModels",
                type: "TEXT",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1024);

            migrationBuilder.AddColumn<byte[]>(
                name: "FileContent",
                table: "PrintModels",
                type: "BLOB",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "PrintModels",
                type: "TEXT",
                maxLength: 512,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileContent",
                table: "PrintModels");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "PrintModels");

            migrationBuilder.AlterColumn<string>(
                name: "FilePath",
                table: "PrintModels",
                type: "TEXT",
                maxLength: 1024,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 1024,
                oldNullable: true);
        }
    }
}
