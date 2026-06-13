using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintCalc.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CompanyId = table.Column<string>(type: "TEXT", nullable: true),
                    VatId = table.Column<string>(type: "TEXT", nullable: true),
                    Street = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: true),
                    Zip = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true),
                    Phone = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FilamentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    DiameterMm = table.Column<decimal>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: true),
                    DensityGPerCm3 = table.Column<decimal>(type: "TEXT", nullable: false),
                    TempMinC = table.Column<int>(type: "INTEGER", nullable: true),
                    TempMaxC = table.Column<int>(type: "INTEGER", nullable: true),
                    AveragePricePerKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilamentTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Printers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "TEXT", nullable: false),
                    KwhPerHour = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaxVolumeDescription = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Printers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FilamentStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilamentTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    SupplierName = table.Column<string>(type: "TEXT", nullable: true),
                    PurchasePricePerKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    InitialWeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    RemainingWeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    PieceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilamentStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilamentStocks_FilamentTypes_FilamentTypeId",
                        column: x => x.FilamentTypeId,
                        principalTable: "FilamentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Calculations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    FilamentTypeId = table.Column<int>(type: "INTEGER", nullable: true),
                    PrinterId = table.Column<int>(type: "INTEGER", nullable: true),
                    SourceModelPath = table.Column<string>(type: "TEXT", nullable: true),
                    DrawingPdfPath = table.Column<string>(type: "TEXT", nullable: true),
                    MaterialGrams = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrintHours = table.Column<decimal>(type: "TEXT", nullable: false),
                    MarginPercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    ElectricityPricePerKwh = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaterialCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrintCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    EnergyCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Subtotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalWithMargin = table.Column<decimal>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calculations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calculations_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Calculations_FilamentTypes_FilamentTypeId",
                        column: x => x.FilamentTypeId,
                        principalTable: "FilamentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Calculations_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrinterFilamentTypes",
                columns: table => new
                {
                    PrinterId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilamentTypeId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrinterFilamentTypes", x => new { x.PrinterId, x.FilamentTypeId });
                    table.ForeignKey(
                        name: "FK_PrinterFilamentTypes_FilamentTypes_FilamentTypeId",
                        column: x => x.FilamentTypeId,
                        principalTable: "FilamentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PrinterFilamentTypes_Printers_PrinterId",
                        column: x => x.PrinterId,
                        principalTable: "Printers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilamentTypeId = table.Column<int>(type: "INTEGER", nullable: false),
                    MovementType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeltaKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPricePerKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FilamentStockId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_FilamentStocks_FilamentStockId",
                        column: x => x.FilamentStockId,
                        principalTable: "FilamentStocks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_FilamentTypes_FilamentTypeId",
                        column: x => x.FilamentTypeId,
                        principalTable: "FilamentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    SourceCalculationId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Calculations_SourceCalculationId",
                        column: x => x.SourceCalculationId,
                        principalTable: "Calculations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Quotes_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "QuoteLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    QuoteId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLines_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Number = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IssueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "TEXT", nullable: false),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invoices_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    TaxRatePercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    LineTotal = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLines_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Calculations_CustomerId",
                table: "Calculations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Calculations_FilamentTypeId",
                table: "Calculations",
                column: "FilamentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Calculations_PrinterId",
                table: "Calculations",
                column: "PrinterId");

            migrationBuilder.CreateIndex(
                name: "IX_FilamentStocks_FilamentTypeId",
                table: "FilamentStocks",
                column: "FilamentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLines_InvoiceId",
                table: "InvoiceLines",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OrderId",
                table: "Invoices",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_QuoteId",
                table: "Orders",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_PrinterFilamentTypes_FilamentTypeId",
                table: "PrinterFilamentTypes",
                column: "FilamentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_QuoteId",
                table: "QuoteLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CustomerId",
                table: "Quotes",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_SourceCalculationId",
                table: "Quotes",
                column: "SourceCalculationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_FilamentStockId",
                table: "StockMovements",
                column: "FilamentStockId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_FilamentTypeId",
                table: "StockMovements",
                column: "FilamentTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "InvoiceLines");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "PrinterFilamentTypes");

            migrationBuilder.DropTable(
                name: "QuoteLines");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "FilamentStocks");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Quotes");

            migrationBuilder.DropTable(
                name: "Calculations");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "FilamentTypes");

            migrationBuilder.DropTable(
                name: "Printers");
        }
    }
}
