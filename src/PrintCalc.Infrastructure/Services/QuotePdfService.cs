using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using PrintCalc.Infrastructure.Rendering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;

namespace PrintCalc.Infrastructure.Services;

public class QuotePdfService : IQuotePdfService
{
    private readonly AppDbContext _db;

    public QuotePdfService(AppDbContext db) => _db = db;

    public async Task<string> SaveQuotePdfAsync(Quote quote, string outputDirectory, CancellationToken ct = default)
    {
        var full = await _db.Quotes
            .Include(q => q.Customer)
            .Include(q => q.Lines)
            .FirstAsync(q => q.Id == quote.Id, ct);

        var companyName = await GetSettingAsync("Company.Name", "Moje 3D firma", ct);
        var companyAddress = await GetSettingAsync("Company.Address", "", ct);
        var companyIco = await GetSettingAsync("Company.Ico", "", ct);
        var companyDic = await GetSettingAsync("Company.Dic", "", ct);
        var companyEmail = await GetSettingAsync("Company.Email", "", ct);
        var companyPhone = await GetSettingAsync("Company.Phone", "", ct);
        var companyIban = await GetSettingAsync("Company.Iban", "", ct);
        var companySwift = await GetSettingAsync("Company.Swift", "", ct);
        var companyBankAccount = await GetSettingAsync("Company.BankAccount", "", ct);
        var paymentMethod = await GetSettingAsync("Company.PaymentMethod", "Převodem", ct);
        var logoPath = await GetSettingAsync("Company.LogoPath", "", ct);
        var visualStyle = await GetSettingAsync("Company.DocumentVisualStyle", "Phoenix", ct);
        var currencySymbol = await GetSettingAsync("Finance.CurrencySymbol", "Kč", ct);
        var accentColor = visualStyle switch
        {
            "Aurora" => Colors.Blue.Medium,
            "Solaris" => Colors.Grey.Darken2,
            _ => Colors.Orange.Medium
        };
        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            logoBytes = await File.ReadAllBytesAsync(logoPath, ct);

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"Nabidka_{full.Number.Replace('/', '-')}.pdf");

        QuestPDF.Settings.License = LicenseType.Community;
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Header().Column(h =>
                {
                    h.Item().Background(accentColor).Padding(10).Text("CENOVÁ NABÍDKA").FontSize(26).FontColor(Colors.White).SemiBold();
                    h.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Číslo cenové nabídky: {full.Number}");
                            c.Item().Text($"Datum vystaveni: {full.IssueDate:yyyy-MM-dd}");
                        });
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Způsob úhrady: {paymentMethod}");
                            c.Item().Text($"Účet - IBAN: {companyIban}");
                            c.Item().Text($"SWIFT (BIC): {companySwift}");
                            c.Item().Text($"Variabilní symbol: {full.Number.Replace("/", "")}");
                        });
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Row(r =>
                    {
                        if (logoBytes is not null)
                            r.ConstantItem(90).AlignMiddle().AlignCenter().Element(e => e.Height(70).Width(70).Image(logoBytes));
                        r.RelativeItem().PaddingRight(10).Column(c =>
                        {
                            c.Item().Text("DODAVATEL").SemiBold();
                            c.Item().Text(companyName);
                            c.Item().Text(companyAddress);
                            c.Item().Text($"IČO: {companyIco}");
                            c.Item().Text($"DIČ: {companyDic}");
                            c.Item().Text($"Kontakt: {companyEmail} {companyPhone}".Trim());
                            c.Item().Text($"Ucet: {companyBankAccount}");
                        });
                        r.RelativeItem().PaddingLeft(10).Column(c =>
                        {
                            c.Item().Text("ODBĚRATEL").SemiBold();
                            c.Item().Text(full.Customer.Name);
                            c.Item().Text(full.Customer.Street ?? "");
                            c.Item().Text($"{full.Customer.Zip} {full.Customer.City}".Trim());
                            c.Item().Text($"IČO: {full.Customer.CompanyId}");
                            c.Item().Text($"DIČ: {full.Customer.VatId}");
                            c.Item().Text($"Kontakt: {full.Customer.Email} {full.Customer.Phone}".Trim());
                        });
                    });

                    col.Item().Table(table =>
                    {
                        var orderedLines = full.Lines.OrderBy(x => x.Id).ToList();
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.75f); // kalk.
                            c.RelativeColumn(3.25f); // polozka
                            c.RelativeColumn(2); // cena
                            c.RelativeColumn(1.5f); // mnoz
                            c.RelativeColumn(2); // spolu
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background(accentColor).Padding(4).Text("KALK.").FontColor(Colors.White).SemiBold().FontSize(9);
                            h.Cell().Background(accentColor).Padding(4).Text("POLOŽKA").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(accentColor).Padding(4).AlignRight().Text("CENA").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(accentColor).Padding(4).AlignRight().Text("MNOŽSTVÍ").FontColor(Colors.White).SemiBold();
                            h.Cell().Background(accentColor).Padding(4).AlignRight().Text("SPOLU BEZ DPH").FontColor(Colors.White).SemiBold();
                        });

                        for (var i = 0; i < orderedLines.Count; i++)
                        {
                            var line = orderedLines[i];
                            var tint = CustomerDocumentLineStripe.RowBackgroundForGroup(orderedLines, i, l => l.SourceCalculationId);
                            void DataCell(IContainer cell, Action<IContainer> inner)
                            {
                                var c = cell;
                                if (tint is not null) c = c.Background(tint.Value);
                                inner(c.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3));
                            }

                            DataCell(table.Cell(), x => x.Text(CustomerDocumentLineStripe.CalcColumnText(line.SourceCalculationId)).FontSize(9));
                            DataCell(table.Cell(), x => x.Text(line.Description));
                            DataCell(table.Cell(), x => x.AlignRight().Text($"{line.UnitPrice:0} {currencySymbol}"));
                            DataCell(table.Cell(), x => x.AlignRight().Text(line.Quantity.ToString("0.##")));
                            DataCell(table.Cell(), x => x.AlignRight().Text($"{line.LineTotal:0} {currencySymbol}"));
                        }
                    });
                    if (full.Lines.Any(l => l.SourceCalculationId is not null))
                        col.Item().Text("Řádky se stejným odstínem a číslem kalkulace patří k jedné kalkulaci (jedné poptávce).").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                    col.Item().AlignRight().Text($"Celkem spolu: {full.TotalAmount:0} {currencySymbol}").Bold().FontSize(16);
                    if (!string.IsNullOrWhiteSpace(full.Notes))
                        col.Item().Text($"Poznámka: {full.Notes}");
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
