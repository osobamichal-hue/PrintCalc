using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using PrintCalc.Infrastructure.Rendering;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.IO;
using System.Text;

namespace PrintCalc.Infrastructure.Services;

public class InvoicePdfService : IInvoicePdfService
{
    private readonly AppDbContext _db;

    public InvoicePdfService(AppDbContext db) => _db = db;

    public async Task<string> SaveInvoicePdfAsync(Invoice invoice, string outputDirectory, CancellationToken ct = default)
    {
        var full = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Lines)
            .FirstAsync(i => i.Id == invoice.Id, ct);

        var companyName = await GetSettingAsync("Company.Name", "Moje 3D firma", ct);
        var companyAddress = await GetSettingAsync("Company.Address", "", ct);
        var companyIco = await GetSettingAsync("Company.Ico", "", ct);
        var companyDic = await GetSettingAsync("Company.Dic", "", ct);
        var companyIban = await GetSettingAsync("Company.Iban", "", ct);
        var companySwift = await GetSettingAsync("Company.Swift", "", ct);
        var paymentMethod = string.IsNullOrWhiteSpace(full.PaymentMethod)
            ? await GetSettingAsync("Company.PaymentMethod", "Převodem", ct)
            : full.PaymentMethod!;
        var companyBankAccount = await GetSettingAsync("Company.BankAccount", "", ct);
        var logoPath = await GetSettingAsync("Company.LogoPath", "", ct);
        var currencySymbol = await GetSettingAsync("Finance.CurrencySymbol", "Kč", ct);

        byte[]? logoBytes = null;
        if (!string.IsNullOrWhiteSpace(logoPath) && File.Exists(logoPath))
            logoBytes = await File.ReadAllBytesAsync(logoPath, ct);

        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, $"Faktura_{full.Number.Replace('/', '-')}.pdf");

        var customerName = full.Customer?.Name ?? $"Zákazník #{full.CustomerId}";
        var customerAddress = $"{full.Customer?.Street} {full.Customer?.Zip} {full.Customer?.City}".Trim();
        var sourceOrderIds = full.Lines.Where(l => l.SourceOrderId.HasValue).Select(l => l.SourceOrderId!.Value).Distinct().ToList();
        var orderInfo = await _db.Orders.AsNoTracking()
            .Where(o => sourceOrderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new { o.Number, o.Title }, ct);
        var net = full.Lines.Sum(x => x.LineTotal);
        var vat = Math.Round(full.Lines.Sum(x => x.LineTotal * (x.TaxRatePercent / 100m)), 0, MidpointRounding.AwayFromZero);
        var gross = Math.Round(net + vat, 0, MidpointRounding.AwayFromZero);
        var variableSymbol = ExtractDigits(full.Number);
        var ibanForQr = ResolveIbanForQr(companyIban, companyBankAccount);
        var dueDate = full.DueDate ?? full.IssueDate.AddDays(14);
        var spd = BuildSpdPaymentString(ibanForQr, gross, variableSymbol, full.Number, currencySymbol, dueDate);
        var qrBytes = CreateQrPng(spd);

        QuestPDF.Settings.License = LicenseType.Community;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().PaddingBottom(10).Row(row =>
                {
                    row.RelativeItem().Column(left =>
                    {
                        if (logoBytes is not null)
                            left.Item().Height(32).Image(logoBytes).FitWidth();
                    });

                    row.RelativeItem().AlignRight().Column(right =>
                    {
                        right.Item().Text($"Faktura {full.Number}").FontSize(22).SemiBold();
                        right.Item().Text("DAŇOVÝ DOKLAD").FontColor(Colors.Grey.Darken1);
                        right.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                    });
                });

                page.Content().Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Row(info =>
                    {
                        info.RelativeItem().Column(supplier =>
                        {
                            supplier.Item().Text("DODAVATEL").FontColor(Colors.Grey.Darken2).SemiBold();
                            supplier.Item().Text(companyName).SemiBold();
                            supplier.Item().Text(companyAddress);
                            supplier.Item().PaddingTop(4).Text($"IČO: {companyIco}");
                            supplier.Item().Text($"DIČ: {companyDic}");
                            supplier.Item().PaddingTop(4).Text($"Bankovní účet: {companyBankAccount}");
                            supplier.Item().Text($"Variabilní symbol: {variableSymbol}");
                            supplier.Item().Text($"Způsob platby: {paymentMethod}");
                            if (!string.IsNullOrWhiteSpace(companyIban))
                                supplier.Item().Text($"IBAN: {companyIban}");
                            if (!string.IsNullOrWhiteSpace(companySwift))
                                supplier.Item().Text($"SWIFT: {companySwift}");
                        });

                        info.RelativeItem().Column(customer =>
                        {
                            customer.Item().Text("ODBĚRATEL").FontColor(Colors.Grey.Darken2).SemiBold();
                            customer.Item().Text(customerName).SemiBold();
                            customer.Item().Text(customerAddress);
                            if (!string.IsNullOrWhiteSpace(full.Customer?.CompanyId))
                                customer.Item().Text($"IČO: {full.Customer.CompanyId}");
                            if (!string.IsNullOrWhiteSpace(full.Customer?.VatId))
                                customer.Item().Text($"DIČ: {full.Customer.VatId}");
                            customer.Item().PaddingTop(4).Text($"Datum vystavení: {full.IssueDate:dd. MM. yyyy}");
                            customer.Item().Text($"Datum splatnosti: {(full.DueDate ?? full.IssueDate.AddDays(14)):dd. MM. yyyy}");
                            customer.Item().Text($"Datum zdan. plnění: {full.IssueDate:dd. MM. yyyy}");
                        });
                    });

                    col.Item().PaddingTop(8).Text("Fakturujeme Vám následující položky").Italic().FontColor(Colors.Grey.Darken1);

                    col.Item().Table(table =>
                    {
                        var orderedLines = full.Lines.OrderBy(l => l.Id).ToList();
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(0.65f);
                            c.RelativeColumn(3.55f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.4f);
                        });

                        table.Header(h =>
                        {
                            h.Cell().PaddingVertical(4).Text("KALK.").SemiBold().FontSize(9);
                            h.Cell().PaddingVertical(4).Text("POLOŽKA").SemiBold();
                            h.Cell().PaddingVertical(4).AlignRight().Text("DPH %").SemiBold();
                            h.Cell().PaddingVertical(4).AlignRight().Text("CENA ZA MJ").SemiBold();
                            h.Cell().PaddingVertical(4).AlignRight().Text("MNOŽSTVÍ").SemiBold();
                            h.Cell().PaddingVertical(4).AlignRight().Text("CELKEM BEZ DPH").SemiBold();
                            h.Cell().ColumnSpan(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });
                        foreach (var orderGroup in orderedLines.GroupBy(l => l.SourceOrderId))
                        {
                            var orderLabel = "Zakázka: neuvedeno";
                            if (orderGroup.Key is int orderId && orderInfo.TryGetValue(orderId, out var info))
                                orderLabel = $"Zakázka {info.Number}: {info.Title}";

                            table.Cell().ColumnSpan(6)
                                .Background(Colors.Grey.Lighten4)
                                .PaddingVertical(3)
                                .PaddingHorizontal(6)
                                .Text(orderLabel)
                                .SemiBold()
                                .FontSize(9);

                            var groupLines = orderGroup.ToList();
                            for (var i = 0; i < groupLines.Count; i++)
                            {
                                var line = groupLines[i];
                                var tint = CustomerDocumentLineStripe.RowBackgroundForGroup(groupLines, i, l => l.SourceCalculationId);

                                void DataCell(IContainer cell, Action<IContainer> inner)
                                {
                                    var c = cell;
                                    if (tint is not null) c = c.Background(tint.Value);
                                    inner(c.PaddingVertical(2).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2));
                                }

                                DataCell(table.Cell(), x => x.Text(CustomerDocumentLineStripe.CalcColumnText(line.SourceCalculationId)).FontSize(9));
                                DataCell(table.Cell(), x => x.Text(line.Description));
                                DataCell(table.Cell(), x => x.AlignRight().Text($"{line.TaxRatePercent:0}"));
                                DataCell(table.Cell(), x => x.AlignRight().Text($"{line.UnitPrice:0} {currencySymbol}"));
                                DataCell(table.Cell(), x => x.AlignRight().Text(line.Quantity.ToString("0.##")));
                                DataCell(table.Cell(), x => x.AlignRight().Text($"{line.LineTotal:0} {currencySymbol}"));
                            }
                        }
                    });

                    col.Item().PaddingTop(12).Row(bottom =>
                    {
                        bottom.ConstantItem(140).Column(qr =>
                        {
                            qr.Item().Height(100).Width(100).Image(qrBytes);
                            qr.Item().Text("QR Platba").FontColor(Colors.Grey.Darken1);
                        });

                        bottom.RelativeItem().AlignRight().Column(sum =>
                        {
                            sum.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });
                                t.Header(h =>
                                {
                                    h.Cell().AlignLeft().Text("SAZBA").FontColor(Colors.Grey.Darken1).SemiBold();
                                    h.Cell().AlignRight().Text("ZÁKLAD").FontColor(Colors.Grey.Darken1).SemiBold();
                                    h.Cell().AlignRight().Text("DPH").FontColor(Colors.Grey.Darken1).SemiBold();
                                });
                                t.Cell().AlignLeft().Text("21 %");
                                t.Cell().AlignRight().Text($"{net:0} {currencySymbol}");
                                t.Cell().AlignRight().Text($"{vat:0} {currencySymbol}");
                            });
                            sum.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                            sum.Item().Text($"Celkem k úhradě: {gross:0} {currencySymbol}").FontSize(16).SemiBold();
                        });
                    });

                    col.Item().PaddingTop(14).AlignRight().Column(sig =>
                    {
                        sig.Item().Text("Razítko a podpis").FontColor(Colors.Grey.Darken1);
                        sig.Item().PaddingTop(24).Row(r =>
                        {
                            r.ConstantItem(180).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });
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

    private static string ExtractDigits(string input)
    {
        var sb = new StringBuilder();
        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
        }

        return sb.Length == 0 ? "0" : sb.ToString();
    }

    private static string BuildSpdPaymentString(string iban, decimal amount, string variableSymbol, string invoiceNumber, string currencySymbol, DateTime dueDate)
    {
        var normalizedIban = NormalizeIban(iban);
        var ccy = currencySymbol.Equals("EUR", StringComparison.OrdinalIgnoreCase) ? "EUR" : "CZK";
        var message = $"Faktura {invoiceNumber}";
        var due = dueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return $"SPD*1.0*ACC:{normalizedIban}*AM:{amount.ToString("0.00", CultureInfo.InvariantCulture)}*CC:{ccy}*X-VS:{variableSymbol}*DT:{due}*MSG:{message}";
    }

    private static string ResolveIbanForQr(string? companyIban, string? companyBankAccount)
    {
        var normalizedIban = NormalizeIban(companyIban ?? string.Empty);
        if (normalizedIban.StartsWith("CZ", StringComparison.OrdinalIgnoreCase) && normalizedIban.Length == 24)
            return normalizedIban;

        var derived = TryBuildCzechIbanFromBankAccount(companyBankAccount ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(derived))
            return derived;

        return normalizedIban;
    }

    private static string NormalizeIban(string value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();

    private static string? TryBuildCzechIbanFromBankAccount(string bankAccount)
    {
        var input = (bankAccount ?? string.Empty).Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(input) || !input.Contains('/'))
            return null;

        var parts = input.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return null;
        var accountPart = parts[0];
        var bankCode = ExtractDigits(parts[1]);
        if (bankCode.Length != 4) return null;

        var accParts = accountPart.Split('-', 2, StringSplitOptions.RemoveEmptyEntries);
        var prefix = accParts.Length == 2 ? ExtractDigits(accParts[0]) : string.Empty;
        var accountNumber = ExtractDigits(accParts.Length == 2 ? accParts[1] : accountPart);
        if (prefix.Length > 6 || accountNumber.Length == 0 || accountNumber.Length > 10)
            return null;

        var bban = $"{bankCode}{prefix.PadLeft(6, '0')}{accountNumber.PadLeft(10, '0')}";
        var rearranged = bban + "CZ00";
        var checksum = 98 - Mod97(rearranged);
        return $"CZ{checksum:00}{bban}";
    }

    private static int Mod97(string input)
    {
        var remainder = 0;
        foreach (var ch in input)
        {
            if (char.IsDigit(ch))
            {
                remainder = (remainder * 10 + (ch - '0')) % 97;
                continue;
            }

            if (!char.IsLetter(ch))
                return -1;

            var value = char.ToUpperInvariant(ch) - 'A' + 10;
            foreach (var digit in value.ToString(CultureInfo.InvariantCulture))
                remainder = (remainder * 10 + (digit - '0')) % 97;
        }
        return remainder;
    }

    private static byte[] CreateQrPng(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }
}
