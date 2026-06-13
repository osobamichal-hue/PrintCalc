using System.Globalization;
using ClosedXML.Excel;
using PrintCalc.Core.Models;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class ExcelInvoiceParser
{
    public static ParsedPurchaseInvoice Parse(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed();
        if (used is null) throw new InvalidOperationException("Excel list je prázdný.");

        var rows = used.RowsUsed().ToList();
        if (rows.Count < 2) throw new InvalidOperationException("Excel musí mít hlavičku a alespoň jeden řádek.");

        var headerRow = rows[0];
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var key = NormalizeHeader(cell.GetString());
            if (!map.ContainsKey(key))
                map[key] = cell.Address.ColumnNumber;
        }

        var result = new ParsedPurchaseInvoice();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var desc = GetCell(row, map, "description", "popis", "nazev", "name", "text");
            if (string.IsNullOrWhiteSpace(desc)) continue;

            var qty = ParseDec(GetCell(row, map, "quantity", "mnozstvi", "qty", "množství")) ?? 1m;
            var unit = GetCell(row, map, "unit", "jednotka", "mj") ?? "ks";
            var unitPrice = ParseDec(GetCell(row, map, "unitprice", "cena", "cena_jednotkova")) ?? 0m;
            var lineTotal = ParseDec(GetCell(row, map, "linetotal", "castka", "celkem", "částka")) ?? unitPrice * qty;
            var vat = ParseDec(GetCell(row, map, "vat", "dph", "taxrate", "sazba_dph")) ?? 21m;

            result.Lines.Add(new ParsedPurchaseInvoiceLine
            {
                Description = desc,
                Quantity = qty,
                Unit = unit,
                UnitPrice = unitPrice,
                TaxRatePercent = vat,
                LineTotal = lineTotal,
                ProductCode = GetCell(row, map, "code", "kod", "productcode", "kód"),
                Ean = GetCell(row, map, "ean", "barcode", "carovy_kod")
            });
        }

        result.Number = GetCell(rows.ElementAtOrDefault(1), map, "number", "cislo", "číslo", "invoice") ?? "";
        result.SupplierName = GetCell(rows.ElementAtOrDefault(1), map, "supplier", "dodavatel") ?? "";
        result.TotalAmount = result.Lines.Sum(l => l.LineTotal);
        return result;
    }

    private static string NormalizeHeader(string h) =>
        h.Trim().ToLowerInvariant().Replace(" ", "_").Replace("-", "_");

    private static string? GetCell(IXLRangeRow row, Dictionary<string, int> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var col))
                return row.Cell(col).GetString().Trim();
        }
        return null;
    }

    private static decimal? ParseDec(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
