using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using PrintCalc.Core.Models;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class CsvInvoiceParser
{
    public static ParsedPurchaseInvoice Parse(Stream stream)
    {
        var result = new ParsedPurchaseInvoice();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.GetCultureInfo("cs-CZ"))
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
            Delimiter = ";",
            TrimOptions = TrimOptions.Trim
        });

        if (!csv.Read() || !csv.ReadHeader())
            throw new InvalidOperationException("CSV soubor neobsahuje hlavičku.");

        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var map = BuildColumnMap(headers);

        while (csv.Read())
        {
            var desc = Get(csv, map, "description", "popis", "nazev", "name", "text");
            if (string.IsNullOrWhiteSpace(desc)) continue;

            var qty = ParseDec(Get(csv, map, "quantity", "mnozstvi", "qty", "množství")) ?? 1m;
            var unit = Get(csv, map, "unit", "jednotka", "mj") ?? "ks";
            var unitPrice = ParseDec(Get(csv, map, "unitprice", "cena", "cena_jednotkova", "cena/j")) ?? 0m;
            var lineTotal = ParseDec(Get(csv, map, "linetotal", "castka", "celkem", "částka", "line_total")) ?? unitPrice * qty;
            var vat = ParseDec(Get(csv, map, "vat", "dph", "taxrate", "sazba_dph")) ?? 21m;

            result.Lines.Add(new ParsedPurchaseInvoiceLine
            {
                Description = desc,
                Quantity = qty,
                Unit = unit,
                UnitPrice = unitPrice,
                TaxRatePercent = vat,
                LineTotal = lineTotal,
                ProductCode = Get(csv, map, "code", "kod", "productcode", "kód"),
                Ean = Get(csv, map, "ean", "barcode", "carovy_kod")
            });
        }

        result.Number = GetFromFirstRow(csv, map, "number", "cislo", "číslo", "invoice", "doklad") ?? "";
        result.SupplierName = GetFromFirstRow(csv, map, "supplier", "dodavatel", "suppliername") ?? "";
        result.SupplierCompanyId = GetFromFirstRow(csv, map, "ico", "supplierico", "supplier_ico");
        result.SupplierVatId = GetFromFirstRow(csv, map, "dic", "supplierdic", "supplier_dic");
        result.TotalAmount = result.Lines.Sum(l => l.LineTotal);

        return result;
    }

    private static Dictionary<string, int> BuildColumnMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Length; i++)
        {
            var key = NormalizeHeader(headers[i]);
            if (!map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    private static string NormalizeHeader(string h) =>
        h.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

    private static string? Get(CsvReader csv, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var idx))
            {
                try { return csv.GetField(idx)?.Trim(); }
                catch { /* ignore */ }
            }
        }
        return null;
    }

    private static string? GetFromFirstRow(CsvReader csv, Dictionary<string, int> map, params string[] keys)
    {
        // Header-level fields may be in dedicated columns on first data row
        return Get(csv, map, keys);
    }

    private static decimal? ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Replace(" ", "").Replace("Kč", "", StringComparison.OrdinalIgnoreCase);
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
