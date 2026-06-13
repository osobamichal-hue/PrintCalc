using System.Globalization;
using System.Text.RegularExpressions;
using PrintCalc.Core.Entities;

namespace PrintCalc.Infrastructure.Services.PurchaseInvoices;

public static class PurchaseInvoiceLineCalculator
{
    private static readonly Regex SpoolWeightRegex = new(@"(?<w>\d+(?:[.,]\d+)?)\s*kg", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void ComputeStockFields(PurchaseInvoiceLine line)
    {
        var unit = (line.Unit ?? "ks").Trim().ToLowerInvariant();
        var spoolKg = ExtractSpoolWeightKg(line.Description);
        decimal weightKg;
        int pieceCount;

        if (unit is "kg" or "kilogram" or "kilogramy")
        {
            weightKg = line.Quantity;
            pieceCount = 1;
        }
        else if (unit is "g" or "gram" or "gramy")
        {
            weightKg = line.Quantity / 1000m;
            pieceCount = 1;
        }
        else
        {
            pieceCount = (int)Math.Max(1, Math.Round(line.Quantity));
            weightKg = pieceCount * (spoolKg ?? 1m);
        }

        line.WeightKg = Math.Round(weightKg, 4, MidpointRounding.AwayFromZero);
        line.PieceCount = pieceCount;

        if (line.WeightKg > 0)
        {
            var total = line.LineTotal > 0 ? line.LineTotal : line.UnitPrice * line.Quantity;
            line.PricePerKg = Math.Round(total / line.WeightKg, 4, MidpointRounding.AwayFromZero);
        }
        else if (unit is "kg" or "kilogram")
        {
            line.PricePerKg = line.UnitPrice;
        }
    }

    public static decimal? ExtractSpoolWeightKg(string description)
    {
        var m = SpoolWeightRegex.Match(description);
        if (!m.Success) return null;
        var s = m.Groups["w"].Value.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var w) ? w : null;
    }

    public static (string? material, string? color, string? manufacturer, decimal diameterMm) ParseDescriptionHints(string description)
    {
        var text = description.ToUpperInvariant();
        string? material = null;
        foreach (var m in new[] { "PLA+", "PLA", "PETG", "ABS", "ASA", "TPU", "NYLON", "PA", "PC", "PVA", "HIPS", "WOOD", "SILK" })
        {
            if (text.Contains(m, StringComparison.Ordinal))
            {
                material = m;
                break;
            }
        }

        var colorMatch = Regex.Match(description, @"\b(bíl[áa]|čern[áa]|červen[áa]|modr[áa]|zelen[áa]|žlut[áa]|oranžov[áa]|šed[áa]|růžov[áa]|fialov[áa]|transparent|natural|black|white|red|blue|green|yellow|orange|grey|gray|pink|purple|silver|gold)\b", RegexOptions.IgnoreCase);
        var color = colorMatch.Success ? colorMatch.Value : null;

        var diaMatch = Regex.Match(description, @"1[.,]75\s*mm|2[.,]85\s*mm|3\s*mm", RegexOptions.IgnoreCase);
        var diameter = diaMatch.Success && diaMatch.Value.Contains('2') && diaMatch.Value.Contains("85") ? 2.85m : 1.75m;

        string? manufacturer = null;
        var known = new[] { "PRUSA", "FILLAMENTUM", "ESUN", "SUNLU", "POLYMAKER", "BAMBU", "CREALITY", "DEVIL DESIGN", "FIBERLOGY", "COLORFABB" };
        foreach (var k in known)
        {
            if (text.Contains(k, StringComparison.Ordinal))
            {
                manufacturer = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(k.ToLower());
                break;
            }
        }

        return (material, color, manufacturer, diameter);
    }
}
