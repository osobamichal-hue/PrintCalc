using PrintCalc.Core.Models;

namespace PrintCalc.Core.Helpers;

public static class QuantityDiscountHelper
{
    public static readonly QuantityDiscountTier[] DefaultTiers =
    [
        new(1, 0m),
        new(5, 5m),
        new(20, 12m)
    ];

    /// <summary>Formát nastavení: „1:0;5:5;20:12“ (min kusů : sleva %).</summary>
    public static IReadOnlyList<QuantityDiscountTier> ParseTiers(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DefaultTiers;

        var tiers = new List<QuantityDiscountTier>();
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf(':');
            if (idx <= 0) continue;
            if (!int.TryParse(part[..idx].Trim(), out var min)) continue;
            if (!decimal.TryParse(part[(idx + 1)..].Trim().Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pct))
                continue;
            tiers.Add(new QuantityDiscountTier(Math.Max(1, min), Math.Max(0, pct)));
        }

        return tiers.Count == 0 ? DefaultTiers : tiers.OrderBy(t => t.MinPieces).ToList();
    }

    public static string FormatTiers(IEnumerable<QuantityDiscountTier> tiers) =>
        string.Join(";", tiers.OrderBy(t => t.MinPieces).Select(t => $"{t.MinPieces}:{t.DiscountPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

    public static decimal ResolveDiscountPercent(int requiredPieces, IReadOnlyList<QuantityDiscountTier> tiers)
    {
        if (tiers.Count == 0) return 0;
        var pieces = Math.Max(1, requiredPieces);
        decimal best = 0;
        foreach (var tier in tiers.OrderBy(t => t.MinPieces))
        {
            if (pieces >= tier.MinPieces)
                best = tier.DiscountPercent;
        }
        return Math.Max(0, best);
    }
}
