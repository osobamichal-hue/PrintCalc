using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace PrintCalc.Infrastructure.Services;

public class CalculationPdfService : ICalculationPdfService
{
    private readonly AppDbContext _db;

    public CalculationPdfService(AppDbContext db) => _db = db;

    public async Task<string> SaveCalculationPdfAsync(Calculation calculation, string outputDirectory, CancellationToken ct = default)
    {
        Calculation full;
        if (calculation.Id > 0)
        {
            full = await _db.Calculations
                .Include(c => c.Customer)
                .Include(c => c.FilamentType)
                .Include(c => c.Printer)
                .FirstAsync(c => c.Id == calculation.Id, ct);
        }
        else
        {
            full = calculation;
        }

        var companyName = await GetSettingAsync("Company.Name", "Moje 3D firma", ct);
        var companyAddress = await GetSettingAsync("Company.Address", "", ct);
        var companyIco = await GetSettingAsync("Company.Ico", "", ct);
        var companyDic = await GetSettingAsync("Company.Dic", "", ct);
        var companyEmail = await GetSettingAsync("Company.Email", "", ct);
        var companyPhone = await GetSettingAsync("Company.Phone", "", ct);
        var logoPath = await GetSettingAsync("Company.LogoPath", "", ct);
        var visualStyle = await GetSettingAsync("Company.DocumentVisualStyle", "Phoenix", ct);
        var currencySymbol = await GetSettingAsync("Finance.CurrencySymbol", "Kč", ct);
        var accentColor = visualStyle switch
        {
            "Aurora" => Colors.Blue.Medium,
            "Solaris" => Colors.Grey.Darken2,
            _ => Colors.Orange.Medium
        };
        var accentSoftColor = visualStyle switch
        {
            "Aurora" => Colors.Blue.Lighten3,
            "Solaris" => Colors.Grey.Lighten3,
            _ => Colors.Orange.Lighten3
        };
        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            logoBytes = await File.ReadAllBytesAsync(logoPath, ct);

        Directory.CreateDirectory(outputDirectory);
        var safeTitle = string.IsNullOrWhiteSpace(full.Title) ? $"Kalkulace_{(full.Id <= 0 ? "draft" : full.Id)}" : full.Title.Replace('/', '-');
        var path = Path.Combine(outputDirectory, $"{safeTitle}.pdf");

        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Header().Column(h =>
                {
                    h.Item().Background(accentColor).Padding(10).Text("KALKULACE CENY").FontColor(Colors.White).FontSize(24).SemiBold();
                    h.Item().PaddingTop(6).Text(companyName).SemiBold();
                    h.Item().Text($"{companyAddress}  IČO: {companyIco}  DIČ: {companyDic}".Trim());
                    h.Item().Text($"{companyEmail}  {companyPhone}".Trim());
                    if (logoBytes is not null)
                        h.Item().Height(36).Width(120).Image(logoBytes);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().Text($"Název: {full.Title}");
                    col.Item().Text($"Datum: {full.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
                    col.Item().Text($"Zákazník: {full.Customer?.Name ?? "—"}");
                    col.Item().Text($"IČO zákazníka: {full.Customer?.CompanyId ?? "—"}");
                    col.Item().Text($"DIČ zákazníka: {full.Customer?.VatId ?? "—"}");
                    col.Item().Text($"Model: {full.SourceModelPath ?? "—"}");
                    col.Item().Text($"Filament: {full.FilamentType?.Name ?? "—"}");
                    col.Item().Text($"Tiskárna: {full.Printer?.Name ?? "—"}");
                    col.Item().Text($"Kusů na podložce: {Math.Max(1, full.PiecesPerBuild)}");
                    col.Item().Text($"Požadovaný počet kusů: {Math.Max(1, full.RequiredPieces)}");
                    col.Item().Text($"Počet opakování tisku: {Math.Max(1, full.PrintRuns)}x");
                    if (full.CustomerSuppliedMaterial)
                        col.Item().Text("Poznámka: tisk z materiálu zákazníka (cena materiálu 0 Kč).").Italic();
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(1);
                        });
                        table.Cell().Background(accentSoftColor).Padding(4).Text("Materiál");
                        table.Cell().Background(accentSoftColor).Padding(4).AlignRight().Text($"{full.MaterialCost:0.00} {currencySymbol}");
                        table.Cell().Padding(4).Column(c =>
                        {
                            c.Item().DefaultTextStyle(t => t.FontSize(9)).Text("Strojní čas (tiskárna)");
                            c.Item().DefaultTextStyle(t => t.FontSize(7).FontColor(Colors.Grey.Darken1))
                                .Text("čas × Kč/h z tiskárny × počet tisků");
                        });
                        table.Cell().Padding(4).AlignRight().Text($"{full.PrintCost:0.00} {currencySymbol}");
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Column(c =>
                        {
                            c.Item().DefaultTextStyle(t => t.FontSize(9)).Text("Energie");
                            c.Item().DefaultTextStyle(t => t.FontSize(7).FontColor(Colors.Grey.Darken1))
                                .Text("čas × kWh/h × Kč/kWh × počet tisků");
                        });
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text($"{full.EnergyCost:0.00} {currencySymbol}");
                        table.Cell().Padding(4).Text("Tvorba modelu");
                        table.Cell().Padding(4).AlignRight().Text($"{full.ModelDesignCost:0.00} {currencySymbol}");
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text("Pevný poplatek (start tisku)");
                        table.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text($"{full.StartFeeCost:0.00} {currencySymbol}");
                        if (full.SlicingFeeCost > 0)
                        {
                            table.Cell().Padding(4).Text("Příprava dat (slicing)");
                            table.Cell().Padding(4).AlignRight().Text($"{full.SlicingFeeCost:0.00} {currencySymbol}");
                        }
                        if (full.PostProcessingCost > 0)
                        {
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text($"Post-processing ({full.PostProcessingHours:0.##} h)");
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text($"{full.PostProcessingCost:0.00} {currencySymbol}");
                        }
                        if (full.WasteCoefficientPercent > 0)
                        {
                            table.Cell().Padding(4).Text($"Koeficient zmetkovitosti ({full.WasteCoefficientPercent:0.#} %)");
                            table.Cell().Padding(4).AlignRight().Text("zahrnuto v materiálu");
                        }
                        table.Cell().Text("Mezisoučet").SemiBold();
                        table.Cell().AlignRight().Text($"{full.Subtotal:0.00} {currencySymbol}").SemiBold();
                        if (full.QuantityDiscountAmount > 0)
                        {
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text($"Množstevní sleva ({full.QuantityDiscountPercent:0.#} %)");
                            table.Cell().Background(Colors.Grey.Lighten4).Padding(4).AlignRight().Text($"-{full.QuantityDiscountAmount:0.00} {currencySymbol}");
                            table.Cell().Padding(4).Text("Po slevě").SemiBold();
                            table.Cell().Padding(4).AlignRight().Text($"{full.DiscountedSubtotal:0.00} {currencySymbol}").SemiBold();
                        }
                        table.Cell().Text("Celkem s marží").Bold();
                        table.Cell().AlignRight().Text($"{full.TotalWithMargin:0.00} {currencySymbol}").Bold();
                        table.Cell().Text("Cena za 1 ks").SemiBold();
                        table.Cell().AlignRight().Text($"{(full.UnitPrice > 0 ? full.UnitPrice : (full.RequiredPieces > 0 ? full.TotalWithMargin / full.RequiredPieces : full.TotalWithMargin)):0.00} {currencySymbol}").SemiBold();
                    });
                });
            });
        }).GeneratePdf(path);

        return path;
    }

    private async Task<string> GetSettingAsync(string key, string fallback, CancellationToken ct)
    {
        var v = await _db.AppSettings.AsNoTracking().Where(x => x.Key == key).Select(x => x.Value).FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(v) ? fallback : v;
    }
}
