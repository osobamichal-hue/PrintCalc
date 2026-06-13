using Microsoft.EntityFrameworkCore;
using PrintCalc.Core.Entities;
using PrintCalc.Core.Services;
using PrintCalc.Infrastructure.Persistence;
using System.Globalization;

namespace PrintCalc.Infrastructure.Services;

public class DocumentNumberService : IDocumentNumberService
{
    private readonly AppDbContext _db;
    private const string DefaultInvoicePrefix = "INV";
    private const int DefaultInvoiceCounterDigits = 3;

    public DocumentNumberService(AppDbContext db) => _db = db;

    public async Task<string> NextQuoteNumberAsync(CancellationToken ct = default)
    {
        var nums = await _db.Quotes.Select(q => q.Number).ToListAsync(ct);
        return Next("QUOTE", nums, digits: 5, useSeparator: true);
    }

    public async Task<string> NextOrderNumberAsync(CancellationToken ct = default)
    {
        var nums = await _db.Orders.Select(o => o.Number).ToListAsync(ct);
        return Next("ORDER", nums, digits: 5, useSeparator: true);
    }

    public async Task<string> NextInvoiceNumberAsync(string? prefixOverride = null, CancellationToken ct = default)
    {
        var settings = await _db.AppSettings.AsNoTracking()
            .Where(x =>
                x.Key == "Finance.InvoiceNumberPrefix" ||
                x.Key == "Finance.InvoiceNumberUseSeparator" ||
                x.Key == "Finance.InvoiceNumberCounterDigits")
            .ToDictionaryAsync(x => x.Key, x => x.Value, ct);

        settings.TryGetValue("Finance.InvoiceNumberPrefix", out var configuredPrefix);
        settings.TryGetValue("Finance.InvoiceNumberUseSeparator", out var configuredUseSeparator);
        settings.TryGetValue("Finance.InvoiceNumberCounterDigits", out var configuredDigits);

        var prefix = NormalizePrefix(prefixOverride, NormalizePrefix(configuredPrefix, DefaultInvoicePrefix));
        var useSeparator = !string.Equals(configuredUseSeparator, "false", StringComparison.OrdinalIgnoreCase);
        var digits = NormalizeCounterDigits(configuredDigits);
        var nums = await _db.Invoices.Select(i => i.Number).ToListAsync(ct);
        var nextSettingsKey = BuildInvoiceSeriesNextKey(prefix);
        var configuredNextRaw = await _db.AppSettings.AsNoTracking()
            .Where(x => x.Key == nextSettingsKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);
        var configuredNext = int.TryParse(configuredNextRaw, out var parsed) && parsed > 0 ? parsed : (int?)null;
        return await NextInvoiceWithSeriesCursorAsync(prefix, nums, digits, useSeparator, nextSettingsKey, configuredNext, ct);
    }

    private static string Next(string prefix, List<string> numbers, int digits, bool useSeparator)
    {
        var max = 0;
        var glue = useSeparator ? "-" : string.Empty;
        var p = prefix + glue;
        foreach (var n in numbers)
        {
            if (!n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
            var part = n[p.Length..];
            if (int.TryParse(part, out var v) && v > max) max = v;
        }

        return $"{prefix}{glue}{(max + 1).ToString($"D{digits}")}";
    }

    private static string NormalizePrefix(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;
        var normalized = value.Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static int NormalizeCounterDigits(string? value)
    {
        if (!int.TryParse(value, out var parsed))
            return DefaultInvoiceCounterDigits;
        return Math.Clamp(parsed, 1, 3);
    }

    private static string BuildInvoiceSeriesNextKey(string prefix)
    {
        var clean = new string(prefix.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        if (string.IsNullOrWhiteSpace(clean))
            clean = "INV";
        return $"Finance.InvoiceSeriesNext.{clean.ToUpperInvariant()}";
    }

    private async Task<string> NextInvoiceWithSeriesCursorAsync(
        string prefix,
        List<string> numbers,
        int digits,
        bool useSeparator,
        string nextSettingsKey,
        int? configuredNext,
        CancellationToken ct)
    {
        var glue = useSeparator ? "-" : string.Empty;
        var p = prefix + glue;
        var used = new HashSet<string>(numbers, StringComparer.OrdinalIgnoreCase);
        var max = 0;
        foreach (var n in numbers)
        {
            if (!n.StartsWith(p, StringComparison.OrdinalIgnoreCase)) continue;
            var part = n[p.Length..];
            if (int.TryParse(part, out var v) && v > max) max = v;
        }

        var candidate = configuredNext ?? (max + 1);
        if (candidate <= 0) candidate = 1;
        while (used.Contains($"{prefix}{glue}{candidate.ToString($"D{digits}")}"))
            candidate++;

        var nextRow = await _db.AppSettings.FirstOrDefaultAsync(x => x.Key == nextSettingsKey, ct);
        if (nextRow is null)
        {
            nextRow = new AppSettingsRow { Key = nextSettingsKey };
            _db.AppSettings.Add(nextRow);
        }
        nextRow.Value = (candidate + 1).ToString(CultureInfo.InvariantCulture);
        await _db.SaveChangesAsync(ct);

        return $"{prefix}{glue}{candidate.ToString($"D{digits}")}";
    }
}
