using System.Globalization;
using System.Text.RegularExpressions;
using PrintCalc.Core.Models;
using UglyToad.PdfPig;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class HeuristicPdfInvoiceParser
{
    public static ParsedPurchaseInvoice Parse(Stream stream)
    {
        using var doc = PdfDocument.Open(stream);
        var text = string.Join("\n", doc.GetPages().Select(p => p.Text));

        var result = new ParsedPurchaseInvoice
        {
            Number = MatchFirst(text, @"(?:Faktura|Doklad|Invoice|FA)[\s.:№#-]*([A-Z0-9\-/]+)", @"(?:Číslo|C\.?\s*f\.?)[\s.:]*([A-Z0-9\-/]+)") ?? "",
            SupplierName = MatchFirst(text, @"(?:Dodavatel|Prodejce|Supplier)[\s:\n]+([^\n]{3,80})", @"(?:DODAVATEL)[^\n]*\n([^\n]{3,80})") ?? "",
            SupplierCompanyId = MatchFirst(text, @"(?:IČO|ICO)[\s:]*(\d{8})"),
            SupplierVatId = MatchFirst(text, @"(?:DIČ|DIC)[\s:]*(CZ\d{8,10})"),
            IssueDate = ParseDate(MatchFirst(text, @"(?:Datum vystavení|Vystaveno|Issue date)[\s:]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})")) ?? DateTime.UtcNow,
            DueDate = ParseDate(MatchFirst(text, @"(?:Datum splatnosti|Splatnost|Due date)[\s:]*(\d{1,2}[./]\d{1,2}[./]\d{2,4})"))
        };

        var lines = ParseLineItems(text);
        result.Lines.AddRange(lines);
        result.TotalAmount = ParseDec(MatchFirst(text, @"(?:Celkem k úhradě|Celkem|Total|K úhradě)[\s:]*([\d\s.,]+)"))
                           ?? result.Lines.Sum(l => l.LineTotal);

        if (result.Lines.Count == 0 && !string.IsNullOrWhiteSpace(text))
        {
            // Fallback: hledat řádky s kg/PLA/PETG
            foreach (Match m in Regex.Matches(text, @"(PLA|PETG|ABS|ASA|TPU)[^\n]{5,120}", RegexOptions.IgnoreCase))
            {
                result.Lines.Add(new ParsedPurchaseInvoiceLine
                {
                    Description = m.Value.Trim(),
                    Quantity = 1,
                    Unit = "ks",
                    UnitPrice = 0,
                    LineTotal = 0,
                    TaxRatePercent = 21
                });
            }
        }

        return result;
    }

    private static List<ParsedPurchaseInvoiceLine> ParseLineItems(string text)
    {
        var lines = new List<ParsedPurchaseInvoiceLine>();
        var rowRegex = new Regex(
            @"^(?<desc>.{5,80}?)\s+(?<qty>\d+(?:[.,]\d+)?)\s+(?<unit>ks|kg|g)\s+(?<price>\d+(?:[.,]\d+)?)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        foreach (Match m in rowRegex.Matches(text))
        {
            var qty = ParseDec(m.Groups["qty"].Value) ?? 1m;
            var price = ParseDec(m.Groups["price"].Value) ?? 0m;
            lines.Add(new ParsedPurchaseInvoiceLine
            {
                Description = m.Groups["desc"].Value.Trim(),
                Quantity = qty,
                Unit = m.Groups["unit"].Value,
                UnitPrice = price,
                LineTotal = qty * price,
                TaxRatePercent = 21
            });
        }

        return lines;
    }

    private static string? MatchFirst(string text, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (m.Success && m.Groups.Count > 1)
                return m.Groups[1].Value.Trim();
        }
        return null;
    }

    private static DateTime? ParseDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("cs-CZ"), DateTimeStyles.None, out var d)) return d;
        return null;
    }

    private static decimal? ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(" ", "").Replace("Kč", "", StringComparison.OrdinalIgnoreCase);
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
