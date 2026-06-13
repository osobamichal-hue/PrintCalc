using System.Text;
using PrintCalc.Core.Entities;

namespace PrintCalc.Core.Helpers;

/// <summary>Srozumitelné titulky a výřezy pro nabídky, zakázky a mřížky (místo „Nabídka (N kalkulací)“).</summary>
public static class DocumentTitleExcerpt
{
    private const int DefaultMaxTotal = 240;
    private const int PartMaxLen = 52;

    public static bool IsGenericMultiCalculationQuoteTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && title.StartsWith("Nabídka (", StringComparison.Ordinal)
        && title.Contains("kalkulací", StringComparison.Ordinal);

    public static bool IsGenericMultiQuoteOrderTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && title.StartsWith("Zakázka (", StringComparison.Ordinal)
        && title.Contains("nabídek", StringComparison.OrdinalIgnoreCase);

    public static string FromCalculationTitles(IEnumerable<Calculation> calculations)
    {
        var names = calculations
            .OrderBy(c => c.CreatedAt)
            .Select(c => string.IsNullOrWhiteSpace(c.Title) ? $"Kalkulace #{c.Id}" : OneLine(c.Title))
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return JoinReadable(names, ", ", maxItems: 6, maxTotal: DefaultMaxTotal);
    }

    public static string FromQuoteLines(IEnumerable<QuoteLine> lines) =>
        FromLineDescriptions(lines.OrderBy(l => l.Id).Select(l => l.Description));

    public static string FromOrderLines(IEnumerable<OrderLine> lines) =>
        FromLineDescriptions(lines.OrderBy(l => l.Id).Select(l => l.Description));

    /// <summary>Řádek v okně výběru nabídek — výcuc místo generického titulu.</summary>
    public static string PickerLabelFromQuote(Quote q)
    {
        if (IsGenericMultiCalculationQuoteTitle(q.Title) && q.Lines is { Count: > 0 })
            return FromQuoteLines(q.Lines);
        if (!string.IsNullOrWhiteSpace(q.Title)) return OneLine(q.Title);
        return q.Number;
    }

    /// <summary>Titulek nové zakázky ze více nabídek.</summary>
    public static string FromQuotesForOrderTitle(IReadOnlyList<Quote> quotes)
    {
        var ordered = quotes.OrderBy(q => q.IssueDate).ToList();
        var parts = new List<string>(ordered.Count);
        foreach (var q in ordered)
        {
            var inner = IsGenericMultiCalculationQuoteTitle(q.Title) && q.Lines is { Count: > 0 }
                ? FromQuoteLines(q.Lines)
                : (!string.IsNullOrWhiteSpace(q.Title) ? OneLine(q.Title) : q.Number);
            parts.Add(Ellipsize(inner, 96));
        }
        return JoinReadable(parts, " | ", maxItems: 6, maxTotal: DefaultMaxTotal);
    }

    /// <summary>Text sloupce „Název“ v seznamu zakázek (DataGrid).</summary>
    public static string ForOrderGridCaption(Order order)
    {
        var t = order.Title?.Trim() ?? "";
        if (order.Lines is { Count: > 0 }
            && (IsGenericMultiCalculationQuoteTitle(t) || IsGenericMultiQuoteOrderTitle(t)))
            return FromOrderLines(order.Lines);
        if (!string.IsNullOrWhiteSpace(t)) return t;
        return string.IsNullOrWhiteSpace(order.Number) ? "Zakázka" : order.Number;
    }

    private static string FromLineDescriptions(IEnumerable<string> descriptions)
    {
        var parts = descriptions
            .Select(OneLine)
            .Where(s => s.Length > 0)
            .Select(s => Ellipsize(s, PartMaxLen))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return JoinReadable(parts, " · ", maxItems: 5, maxTotal: DefaultMaxTotal);
    }

    private static string JoinReadable(IReadOnlyList<string> parts, string separator, int maxItems, int maxTotal)
    {
        var nonEmpty = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var hadMore = nonEmpty.Count > maxItems;
        var list = nonEmpty.Take(maxItems).ToList();
        if (list.Count == 0) return "";
        var sb = new StringBuilder();
        for (var i = 0; i < list.Count; i++)
        {
            if (i > 0) sb.Append(separator);
            var p = list[i];
            if (sb.Length + p.Length > maxTotal)
            {
                var room = maxTotal - sb.Length - 1;
                if (room > 6) sb.Append(p.AsSpan(0, Math.Min(p.Length, room)));
                sb.Append('…');
                return sb.ToString();
            }
            sb.Append(p);
        }
        if (hadMore && sb.Length + 1 <= maxTotal)
            sb.Append('…');
        return sb.ToString();
    }

    private static string OneLine(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Replace('\r', ' ').Replace('\n', ' ').Trim();
        while (t.Contains("  ", StringComparison.Ordinal)) t = t.Replace("  ", " ", StringComparison.Ordinal);
        return t;
    }

    private static string Ellipsize(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Length <= maxLen) return s;
        return maxLen <= 1 ? "…" : s[..(maxLen - 1)] + "…";
    }
}
