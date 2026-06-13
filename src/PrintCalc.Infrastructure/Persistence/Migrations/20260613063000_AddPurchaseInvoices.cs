using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PrintCalc.Infrastructure.Persistence;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260613063000_AddPurchaseInvoices")]
    public partial class AddPurchaseInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CompanyId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    VatId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Aliases = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: true),
                    SupplierName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    SupplierCompanyId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SupplierVatId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportSource = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceFileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    SourceFilePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseInvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PurchaseInvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    ProductCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Ean = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FilamentTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    MatchStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchConfidence = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    PricePerKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    PieceCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseInvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceLines_FilamentTypes_FilamentTypeId",
                        column: x => x.FilamentTypeId,
                        principalTable: "FilamentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PurchaseInvoiceLines_PurchaseInvoices_PurchaseInvoiceId",
                        column: x => x.PurchaseInvoiceId,
                        principalTable: "PurchaseInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceLineId",
                table: "FilamentStocks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseInvoiceLineId",
                table: "StockMovements",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilamentStocks_PurchaseInvoiceLineId",
                table: "FilamentStocks",
                column: "PurchaseInvoiceLineId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_FilamentTypeId",
                table: "PurchaseInvoiceLines",
                column: "FilamentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoiceLines_PurchaseInvoiceId",
                table: "PurchaseInvoiceLines",
                column: "PurchaseInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseInvoices_SupplierId",
                table: "PurchaseInvoices",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseInvoiceLineId",
                table: "StockMovements",
                column: "PurchaseInvoiceLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseInvoiceLines");

            migrationBuilder.DropTable(
                name: "PurchaseInvoices");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_FilamentStocks_PurchaseInvoiceLineId",
                table: "FilamentStocks");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseInvoiceLineId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceLineId",
                table: "FilamentStocks");

            migrationBuilder.DropColumn(
                name: "PurchaseInvoiceLineId",
                table: "StockMovements");
        }
    }
}
